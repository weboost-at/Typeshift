using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Typeshift;

// One run of a conversation: the messages it stored, by reference, since the tool loop hands the same ones on; the
// steering input waiting for the next model call, and where in the tool loop's messages that input went
internal sealed class ConversationRun(
    object conversation,
    string agent,
    string model,
    IStoreAccess store,
    TimeProvider time,
    ILogger logger)
{
    private readonly ConcurrentQueue<ChatMessage> _steering = new();
    private readonly List<(int Position, ChatMessage Message)> _injected = [];
    private readonly HashSet<ChatMessage> _stored = new(ReferenceEqualityComparer.Instance);

    // A streamed response is stored as the updates make it; the tool loop builds its own messages from the same
    // updates, so the first ones it hands on next are those, already stored
    private int _streamedUnmatched;

    public object Conversation { get; } = conversation;

    public string Agent { get; } = agent;

    public string Model { get; } = model;

    public DateTimeOffset StartedAt { get; } = time.GetUtcNow();

    public bool IsSteered => !_steering.IsEmpty;

    public void Steer(ChatMessage input) => _steering.Enqueue(input);

    // The history and the input a run starts from are stored already
    public void MarkStored(IEnumerable<ChatMessage> messages)
    {
        foreach (var message in messages)
        {
            _stored.Add(message);
        }
    }

    // The tool loop's list with the steering input put back where it went in; the steps not stored yet (tool results)
    // are stored, then the steering input waiting is taken in and stored (D51)
    public async Task<List<ChatMessage>> PrepareCallAsync(IEnumerable<ChatMessage> messages)
    {
        var call = messages.ToList();

        foreach (var (position, message) in _injected)
        {
            call.Insert(Math.Min(position, call.Count), message);
        }

        var steps = call.Where(message => !_stored.Contains(message)).ToList();
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
        bool endsRun = false)
    {
        foreach (var message in messages)
        {
            if (message.Role != ChatRole.User) message.AuthorName ??= Agent;
            _stored.Add(message);
        }

        var entry = new ConversationEntry(Agent, messages, time.GetUtcNow())
        {
            Model = model,
            Usage = usage,
            EndsRun = endsRun
        };

        try
        {
            await store.AppendConversationAsync(Conversation, entry, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Conversation {Conversation} could not store a step.", Conversation);
        }
    }
}
