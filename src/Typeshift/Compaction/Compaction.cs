using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D32: what compaction did in one run, applied again to each later call, since the tool loop keeps the whole
// history: the results it cleared, and the summary that stands in for the turns before the first message it kept.
// The estimate is calibrated by the tokens the provider reported for the run's last call, and before the run's first
// call by the last call of the conversation that holds both an estimate and a report
internal sealed class Compaction(ReportedCall? last)
{
    public const string ClearedResult = "[Cleared to save context.]";

    // D52: only a tool marked read-only is safe to call again; any other would send, pay or delete a second time
    public const string ClearedReadOnlyResult =
        "[Cleared to save context; call the tool again if you need this result.]";

    // D32: the latest call of a tool named write_todos and its result stay, whatever goes
    public const string WriteTodos = "write_todos";

    private const string SummaryInstructions =
        """
        Summarize the conversation below for the assistant that continues it. Keep what it needs to go on: the
        task, the decisions made and why, facts learned, files and names touched, and the work left to do. Write
        plain text, without a preamble.
        """;

    // shortcut: a result longer than this is cut in the transcript the summary model reads, make it an option when
    // a summary has to keep what a long result said
    private const int TranscriptResultLength = 2_000;

    // The placeholder of each cleared result, by its call
    private readonly Dictionary<string, string> _cleared = [];
    private List<ChatMessage> _summary = [];
    private AIContent? _keptFrom;
    private double _calibration = last?.Calibration ?? 1;

    // The estimate of the run's last call as it went to the model, with the results cleared and the summary in
    // place: what the provider's report is compared with, and what the response's entry keeps for the next run
    public int SentEstimate { get; private set; }

    public async Task<List<ChatMessage>> PrepareAsync(
        IEnumerable<ChatMessage> messages,
        ResolvedChatOptions options,
        int max,
        CancellationToken ct)
    {
        var history = Apply(messages, options);
        List<ChatMessage> call = [.. history.Select(Clear)];
        SentEstimate = Tokens.Estimate(call, options);
        if (SentEstimate * _calibration < max) return call;

        var target = options.RunOptions.TargetInputTokens ?? max / 2;
        var (count, estimate) = (call.Count, SentEstimate);
        call = await CompactAsync(history, SentEstimate, options, target / _calibration, ct);
        SentEstimate = Tokens.Estimate(call, options);

        // A call with nothing left to clear or to summarize goes out as it came, and says nothing
        if (SentEstimate >= estimate) return call;

        var run = options.AgentRun!;
        LogEvents.Compacted(LogEvents.For(run.Services), run.Agent.Name, count, estimate, call.Count, SentEstimate);

        return call;
    }

    public void Calibrate(UsageDetails? usage)
    {
        if (usage?.InputTokenCount is { } reported && SentEstimate > 0)
            _calibration = new ReportedCall(SentEstimate, reported).Calibration;
    }

    // The history with the summary in place of what it stands for, in each pass of the run, and every result as its
    // tool returned it. What the pass added ahead of the kept message stays, as the pass has it now. A call without
    // the message the summary kept from is compacted afresh
    private List<ChatMessage> Apply(IEnumerable<ChatMessage> messages, ResolvedChatOptions options)
    {
        var call = messages.ToList();

        if (_keptFrom is { } kept)
        {
            var index = call.FindIndex(message => message.Contents.Any(content => ReferenceEquals(content, kept)));

            if (index >= 0)
            {
                var added = call.Take(index).Where(message => IsAdded(message, options));
                call = [.. _summary, .. added, .. call.Skip(index)];
            }
            else
            {
                _summary = [];
                _keptFrom = null;
            }
        }

        return call;
    }

    private async Task<List<ChatMessage>> CompactAsync(
        List<ChatMessage> history,
        double total,
        ResolvedChatOptions options,
        double limit,
        CancellationToken ct)
    {
        // First the results of the oldest calls, keeping the calls; the model has not seen those after its last answer
        var seen = history.FindLastIndex(message => message.Role == ChatRole.Assistant);
        var todos = LatestTodos(history)?.CallId;
        var read = history.Take(Math.Max(seen, 0)).SelectMany(message => message.Contents).ToList();
        var tools = read.OfType<FunctionCallContent>().ToLookup(called => called.CallId, called => called.Name);

        foreach (var result in read.OfType<FunctionResultContent>())
        {
            if (total <= limit) break;
            if (result.CallId == todos || _cleared.ContainsKey(result.CallId)) continue;

            // A result no longer than its placeholder stays: clearing it would save nothing and cost its value
            var placeholder = Placeholder(tools[result.CallId].FirstOrDefault(), options);
            var saved = Tokens.Estimate(result) - (placeholder.Length / 4);
            if (saved <= 0) continue;

            _cleared[result.CallId] = placeholder;
            total -= saved;
        }

        List<ChatMessage> call = [.. history.Select(Clear)];
        if (total <= limit) return call;

        // Then the oldest turns, summarized, up to where the rest fits with room for the summary
        if (Cut(call, options, limit * 0.9) is not { } cut) return call;

        var region = call.Take(cut).ToList();

        // The summary model reads the turns with the results clearing took from the run's model, and none of what
        // the pass added. Without a summary the call goes out with its results cleared, and the provider decides
        List<ChatMessage> turns = [.. history.Take(cut).Where(message => !IsAdded(message, options))];
        if (await TrySummarizeAsync(turns, options, ct) is not { } summary) return call;

        _summary = [summary, .. TodosOf(region)];
        var keptContents = call[cut].Contents;

        // A streamed pass is rebuilt from its updates, with text merged into new contents; calls keep their instances
        _keptFrom = keptContents.OfType<FunctionCallContent>().FirstOrDefault() ?? keptContents[0];

        var kept = call.Skip(cut).ToList();

        if (options.ConversationRun is { } conversation)
        {
            await conversation.AppendSummaryAsync(
                [.. _summary, .. kept.Where(message => !conversation.IsTransient(message))]);
        }

        return [.. _summary, .. region.Where(message => IsAdded(message, options)), .. kept];
    }

    // D31, D30: what a context provider added, or run middleware to the messages it received, is the pass's own, like
    // the instructions: it stays in every call, and no summary reads or stores it
    private static bool IsAdded(ChatMessage message, ResolvedChatOptions options) =>
        options.ConversationRun?.IsAdded(message) is true;

    // The earliest place to cut after which the rest fits, or else the latest: never inside the summary of an
    // earlier compaction, never between a call and its result, never at a message without contents or one the pass
    // added, and only with a turn to summarize ahead. What the pass added ahead stays, so it counts as the tools do
    private int? Cut(List<ChatMessage> call, ResolvedChatOptions options, double limit)
    {
        var stays = (double)Tokens.Estimate([], options);
        var tail = new double[call.Count + 1];

        for (var i = call.Count - 1; i >= 0; i--)
        {
            tail[i] = tail[i + 1] + call[i].Contents.Sum(Tokens.Estimate);
        }

        var open = new HashSet<string>();
        var summarized = false;
        int? latest = null;

        for (var i = 0; i < call.Count; i++)
        {
            var added = IsAdded(call[i], options);

            var allowed = summarized
                && !added
                && i >= _summary.Count
                && open.Count == 0
                && call[i].Role != ChatRole.Tool
                && call[i].Contents.Count > 0;

            if (allowed && stays + tail[i] <= limit) return i;
            if (allowed) latest = i;

            if (added) stays += tail[i] - tail[i + 1];
            else summarized = true;

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
        cleared.Contents = [.. message.Contents.Select(Cleared)];
        return cleared;
    }

    private bool IsCleared(AIContent content) =>
        content is FunctionResultContent result && _cleared.ContainsKey(result.CallId);

    private AIContent Cleared(AIContent content) =>
        content is FunctionResultContent result && _cleared.TryGetValue(result.CallId, out var placeholder)
            ? new FunctionResultContent(result.CallId, placeholder)
            : content;

    private static string Placeholder(string? tool, ChatOptions options) =>
        options.Tools?.FirstOrDefault(offered => offered.Name == tool) is { } called
        && ReadOnlyFunction.IsReadOnly(called)
            ? ClearedReadOnlyResult
            : ClearedResult;

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

    // D77: a summary is best effort. The provider and model named for it write it, the run's own unless the settings
    // or the agent name others, and when they fail the run's own; D63: none for a run on the client registered for
    // every agent. Each failure is logged and recorded on the run's span, and null says that no model wrote one
    private static async Task<ChatMessage?> TrySummarizeAsync(
        List<ChatMessage> turns,
        ResolvedChatOptions options,
        CancellationToken ct)
    {
        var run = options.AgentRun!;
        (ProviderId? Provider, string? Model) own = (options.Provider, options.ModelId);
        var named = (options.SummaryProvider ?? own.Provider, options.RunOptions.SummaryModel ?? own.Model);

        foreach (var (provider, model) in named == own ? [own] : new[] { named, own })
        {
            try
            {
                return await SummarizeAsync(turns, provider, model, run, ct);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                run.Activity?.AddException(exception);

                LogEvents.SummaryFailed(
                    LogEvents.For(run.Services),
                    exception,
                    run.Agent.Name,
                    model,
                    provider,
                    (provider, model) == own
                        ? "its call goes out without a summary, the oldest tool results cleared"
                        : "its own model writes the summary instead");
            }
        }

        return null;
    }

    private static async Task<ChatMessage> SummarizeAsync(
        List<ChatMessage> turns,
        ProviderId? provider,
        string? model,
        AgentRun run,
        CancellationToken ct)
    {
        var instrumented = run.Services.GetRequiredService<InstrumentedClients>();

        var provided = await ChatClients.GetAsync(run.Services, provider, model, ct)
            ?? throw new InvalidOperationException(
                ChatClients.NoClient($"the summary of the agent '{run.Agent.Name}'", provider, model));
        using var client = instrumented.Instrument(provided, model);

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.System, SummaryInstructions), new ChatMessage(ChatRole.User, Transcript(turns))],
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
