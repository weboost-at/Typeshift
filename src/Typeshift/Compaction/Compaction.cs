using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D32: what compaction did in one run, applied again to each later call, since the tool loop keeps the whole
// history: the results it cleared, and the summary that stands in for the turns before the first message it kept.
// The estimate is calibrated by the tokens the provider reported for the run's last call
internal sealed class Compaction
{
    public const string ClearedResult = "[Cleared to save context; call the tool again if you need this result.]";

    // D32, D37: the latest write_todos call and its result stay, whatever goes
    public const string WriteTodos = "write_todos";

    private const string SummaryInstructions =
        """
        Summarize the conversation below for the assistant that continues it. Keep what it needs to go on: the
        task, the decisions made and why, facts learned, files and names touched, and the work left to do. Write
        plain text, without a preamble.
        """;

    // shortcut: a result longer than this is cut in the transcript the summary model reads
    private const int TranscriptResultLength = 2_000;

    private readonly HashSet<string> _cleared = [];
    private List<ChatMessage> _summary = [];
    private AIContent? _keptFrom;
    private double _calibration = 1;

    public async Task<List<ChatMessage>> PrepareAsync(
        IEnumerable<ChatMessage> messages,
        ResolvedChatOptions options,
        int max,
        CancellationToken ct)
    {
        var call = Apply(messages);
        if (Tokens.Estimate(call, options) * _calibration < max) return call;

        var target = options.RunOptions.TargetInputTokens ?? max / 2;
        return await CompactAsync(call, options, target / _calibration, ct);
    }

    public void Calibrate(IReadOnlyList<ChatMessage> call, ChatOptions options, UsageDetails? usage)
    {
        var estimate = Tokens.Estimate(call, options);

        if (usage?.InputTokenCount is { } reported && estimate > 0)
            _calibration = Math.Clamp(reported / (double)estimate, 0.25, 4);
    }

    // The call as the model saw it last: the summary in place of what it stands for, the cleared results cleared. A
    // call without the message the summary kept from (a steered run's later pass) is compacted afresh
    private List<ChatMessage> Apply(IEnumerable<ChatMessage> messages)
    {
        var call = messages.ToList();

        if (_keptFrom is { } kept)
        {
            var index = call.FindIndex(message => message.Contents.Any(content => ReferenceEquals(content, kept)));

            if (index >= 0)
            {
                call = [.. _summary, .. call.Skip(index)];
            }
            else
            {
                _summary = [];
                _keptFrom = null;
            }
        }

        return [.. call.Select(Clear)];
    }

    private async Task<List<ChatMessage>> CompactAsync(
        List<ChatMessage> call,
        ResolvedChatOptions options,
        double limit,
        CancellationToken ct)
    {
        var total = (double)Tokens.Estimate(call, options);

        // First the results of the oldest calls, keeping the calls; the model has not seen those after its last answer
        var seen = call.FindLastIndex(message => message.Role == ChatRole.Assistant);
        var todos = LatestTodos(call)?.CallId;

        foreach (var result in call.Take(Math.Max(seen, 0)).SelectMany(message => message.Contents)
                     .OfType<FunctionResultContent>())
        {
            if (total <= limit) break;
            if (result.CallId == todos || !_cleared.Add(result.CallId)) continue;

            total -= Tokens.Estimate(result) - (ClearedResult.Length / 4);
        }

        call = [.. call.Select(Clear)];
        if (total <= limit) return call;

        // Then the oldest turns, summarized, up to where the rest fits with room for the summary
        if (Cut(call, options, limit * 0.9) is not { } cut) return call;

        var region = call.Take(cut).ToList();

        // D56: the run's provider and model unless the settings or the agent name others; a run has both
        var summary = await SummarizeAsync(
            region,
            options.SummaryProvider ?? options.Provider!,
            options.RunOptions.SummaryModel ?? options.ModelId!,
            options.AgentRun!,
            ct);

        _summary = [summary, .. TodosOf(region)];
        _keptFrom = call[cut].Contents[0];

        var kept = call.Skip(cut).ToList();

        if (options.ConversationRun is { } conversation)
        {
            await conversation.AppendSummaryAsync(
                [.. _summary, .. kept.Where(message => !conversation.IsTransient(message))]);
        }

        return [.. _summary, .. kept];
    }

    // The earliest place to cut after which the rest fits, or else the latest: never inside the summary of an
    // earlier compaction, never between a call and its result, never at a message without contents
    private int? Cut(List<ChatMessage> call, ResolvedChatOptions options, double limit)
    {
        var overhead = Tokens.Estimate([], options);
        var tail = new double[call.Count + 1];

        for (var i = call.Count - 1; i >= 0; i--)
        {
            tail[i] = tail[i + 1] + call[i].Contents.Sum(Tokens.Estimate);
        }

        var open = new HashSet<string>();
        int? latest = null;

        for (var i = 0; i < call.Count; i++)
        {
            var allowed = i > 0
                && i >= _summary.Count
                && open.Count == 0
                && call[i].Role != ChatRole.Tool
                && call[i].Contents.Count > 0;

            if (allowed && overhead + tail[i] <= limit) return i;
            if (allowed) latest = i;

            foreach (var content in call[i].Contents)
            {
                if (content is FunctionCallContent called) open.Add(called.CallId);
                if (content is FunctionResultContent result) open.Remove(result.CallId);
            }
        }

        return latest;
    }

    private ChatMessage Clear(ChatMessage message)
    {
        if (!message.Contents.Any(IsCleared)) return message;

        var cleared = message.Clone();

        cleared.Contents =
        [
            .. message.Contents.Select(content => IsCleared(content)
                ? new FunctionResultContent(((FunctionResultContent)content).CallId, ClearedResult)
                : content)
        ];

        return cleared;
    }

    private bool IsCleared(AIContent content) =>
        content is FunctionResultContent result && _cleared.Contains(result.CallId);

    private static FunctionCallContent? LatestTodos(IEnumerable<ChatMessage> messages) =>
        messages.SelectMany(message => message.Contents)
            .OfType<FunctionCallContent>()
            .LastOrDefault(call => call.Name == WriteTodos);

    // The latest write_todos call among what goes, with its result, as messages of their own
    private static List<ChatMessage> TodosOf(List<ChatMessage> region)
    {
        if (LatestTodos(region) is not { } call) return [];

        var result = region.SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .FirstOrDefault(result => result.CallId == call.CallId);

        return result is null
            ? []
            : [new ChatMessage(ChatRole.Assistant, [call]), new ChatMessage(ChatRole.Tool, [result])];
    }

    private static async Task<ChatMessage> SummarizeAsync(
        List<ChatMessage> region,
        object provider,
        string model,
        AgentRun run,
        CancellationToken ct)
    {
        var clients = run.Services.GetRequiredService<ChatClients>();
        var instrumented = run.Services.GetRequiredService<InstrumentedClients>();
        using var client = instrumented.Instrument(await clients.GetAsync(run.Services, provider, model, ct));

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.System, SummaryInstructions), new ChatMessage(ChatRole.User, Transcript(region))],
            new ChatOptions { ModelId = model },
            ct);

        return new ChatMessage(ChatRole.User, $"A summary of the conversation so far:\n\n{response.Text}");
    }

    private static string Transcript(IEnumerable<ChatMessage> messages)
    {
        var transcript = new StringBuilder();

        foreach (var message in messages)
        {
            foreach (var content in message.Contents)
            {
                var line = content switch
                {
                    TextContent text => text.Text,
                    FunctionCallContent call => $"calls {call.Name}({Tokens.Text(call.Arguments)})",
                    FunctionResultContent result => $"result: {Shorten(Tokens.Text(result.Result))}",
                    _ => null
                };

                if (line is not null) transcript.AppendLine($"{message.Role}: {line}");
            }
        }

        return transcript.ToString();
    }

    private static string Shorten(string text) =>
        text.Length <= TranscriptResultLength ? text : $"{text[..TranscriptResultLength]}…";
}
