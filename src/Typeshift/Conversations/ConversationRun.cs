using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Typeshift;

// One run of a conversation: the contents it stored, by reference, since the tool loop hands the same ones on, even
// in the copies of messages it makes; the steering input waiting for the next model call, and where in the tool
// loop's messages that input went
internal sealed class ConversationRun(
    object conversation,
    AgentRun run,
    string model,
    IStoreAccess store,
    TimeProvider time,
    ILogger logger)
{
    private readonly ConcurrentQueue<ChatMessage> _steering = new();
    private readonly List<(int Position, ChatMessage Message)> _injected = [];
    private readonly HashSet<AIContent> _stored = new(ReferenceEqualityComparer.Instance);

    // What context providers and run middleware added, which the conversation never stores
    private readonly HashSet<AIContent> _transient = new(ReferenceEqualityComparer.Instance);

    // A streamed response is stored as the updates make it; the tool loop builds its own messages from the same
    // updates, so the first ones it hands on next are those, already stored
    private int _streamedUnmatched;

    public object Conversation { get; } = conversation;

    public string Agent { get; } = run.Agent.Name;

    public string Model { get; } = model;

    public DateTimeOffset StartedAt { get; } = time.GetUtcNow();

    public bool IsSteered => !_steering.IsEmpty;

    public void Steer(ChatMessage input) => _steering.Enqueue(input);

    // The history and the input a run starts from are stored already
    public void MarkStored(IEnumerable<ChatMessage> messages)
    {
        foreach (var content in messages.SelectMany(message => message.Contents))
        {
            _stored.Add(content);
        }
    }

    public void MarkTransient(IEnumerable<ChatMessage> messages)
    {
        var added = messages.SelectMany(message => message.Contents).Where(content => !_stored.Contains(content));

        foreach (var content in added)
        {
            _transient.Add(content);
        }
    }

    public bool IsTransient(ChatMessage message) => message.Contents.Any(_transient.Contains);

    // The tool loop's list with the steering input put back where it went in; the steps not stored yet (tool results)
    // are stored, then the steering input waiting is taken in and stored (D51)
    public async Task<List<ChatMessage>> PrepareCallAsync(IEnumerable<ChatMessage> messages)
    {
        var call = messages.ToList();

        foreach (var (position, message) in _injected)
        {
            call.Insert(Math.Min(position, call.Count), message);
        }

        var steps = call.Select(Unstored).OfType<ChatMessage>().ToList();
        var streamed = Math.Min(_streamedUnmatched, steps.Count);

        MarkStored(steps.Take(streamed));
        _streamedUnmatched = 0;

        if (steps.Count > streamed) await AppendAsync([.. steps.Skip(streamed)]);

        var steering = new List<ChatMessage>();

        while (_steering.TryDequeue(out var input))
        {
            _injected.Add((call.Count, input));
            call.Add(input);
            steering.Add(input);
        }

        if (steering.Count > 0) await AppendAsync(steering);
        return call;
    }

    // D46: the end of a run is an entry of its own, with what went wrong and the mode it ended in (D52)
    public Task EndAsync(string? error) =>
        AppendAsync(
            error is null ? [] : [new ChatMessage(ChatRole.Assistant, [new ErrorContent(error)])],
            endsRun: true);

    // D32: what compaction left of the history, which later runs start from
    public Task AppendSummaryAsync(IReadOnlyList<ChatMessage> messages) => AppendAsync(messages, summary: true);

    public async Task AppendResponseAsync(ChatResponse response, ChatOptions? options, bool streamed)
    {
        await AppendAsync([.. response.Messages], response.ModelId ?? options?.ModelId, response.Usage);
        if (streamed) _streamedUnmatched = response.Messages.Count;
    }

    // D46: storing never observes the run's token, and a store that fails is logged while the run goes on
    public async Task AppendAsync(
        IReadOnlyList<ChatMessage> messages,
        string? model = null,
        UsageDetails? usage = null,
        bool endsRun = false,
        bool summary = false)
    {
        foreach (var message in messages)
        {
            if (message.Role != ChatRole.User) message.AuthorName ??= Agent;
        }

        MarkStored(messages);

        var entry = new ConversationEntry(Agent, messages, time.GetUtcNow())
        {
            Model = model,
            Usage = usage,
            EndsRun = endsRun,
            Mode = endsRun ? run.Mode : null,
            IsSummary = summary
        };

        try
        {
            await store.AppendConversationAsync(Conversation, entry, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Conversation {Conversation} could not store a step.", Conversation);
            run.Activity?.AddException(exception);
        }
    }

    // The contents of a message not stored yet, as a message of their own; null when all are
    private ChatMessage? Unstored(ChatMessage message)
    {
        if (message.Contents.All(IsKnown)) return null;
        if (!message.Contents.Any(IsKnown)) return message;

        var step = message.Clone();
        step.Contents = [.. message.Contents.Where(content => !IsKnown(content))];
        return step;
    }

    private bool IsKnown(AIContent content) => _stored.Contains(content) || _transient.Contains(content);
}
