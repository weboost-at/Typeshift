using System.Collections.Concurrent;
using Microsoft.Extensions.AI;

namespace Typeshift.Conversations;

/// <summary>
/// Tracks the sessions of every conversation while they run and, when a store is registered, stores them when they
/// end under the conversation's id.
/// </summary>
public sealed class ConversationTracker
{
    internal static readonly TimeSpan SubscriberLimit = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<Guid, TrackedConversationSession> _sessions = [];
    private readonly ConversationStore? _store;
    private readonly TimeProvider _time;

    // Sessions of one conversation store one at a time, or two ending at once would append to the same load
    private readonly SemaphoreSlim[] _storeLocks = [.. Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1))];

    internal ConversationTracker(ConversationStore? store, TimeProvider time)
    {
        _store = store;
        _time = time;
    }

    /// <summary>
    /// The running sessions that do not stream.
    /// </summary>
    public IReadOnlyList<TrackedConversationSession> Sessions =>
        [.. _sessions.Values.Where(session => session is not RealtimeTrackedConversationSession)];

    /// <summary>
    /// The running streaming sessions, which publish their updates.
    /// </summary>
    public IReadOnlyList<RealtimeTrackedConversationSession> RealtimeSessions =>
        [.. _sessions.Values.OfType<RealtimeTrackedConversationSession>()];

    /// <summary>
    /// Raised when a session starts or ends; handlers must honour the token, they get five seconds.
    /// </summary>
    public event Func<CancellationToken, Task>? OnConversationsChanged;

    internal Task<TrackedConversationSession> TrackSessionAsync(ConversationStart start, CancellationToken ct) =>
        TrackAsync(start, resumed => new TrackedConversationSession(start, resumed, _time), ct);

    internal Task<RealtimeTrackedConversationSession> TrackRealtimeSessionAsync(
        ConversationStart start,
        CancellationToken ct) =>
        TrackAsync(start, resumed => new RealtimeTrackedConversationSession(start, resumed, _time), ct);

    // Storing what happened is not cancelled with the call, a cancelled call is stored as well
    internal async Task EndSessionAsync(Guid sessionId, ChatResponse response)
    {
        if (!_sessions.TryRemove(sessionId, out var tracked)) return;

        var completed = await tracked.CompleteAsync(response);
        if (StoreOf(tracked.ConversationId) is { } store) await StoreAsync(store, tracked, completed);

        await Subscribers.RaiseAsync(OnConversationsChanged, SubscriberLimit, _time);
    }

    // An id that is not stored starts a new conversation under that id
    private async Task<TSession> TrackAsync<TSession>(
        ConversationStart start,
        Func<StoredConversation?, TSession> create,
        CancellationToken ct)
        where TSession : TrackedConversationSession
    {
        var resumed = StoreOf(start.ConversationId) is { } store
            ? await store.LoadAsync(start.ConversationId!, ct)
            : null;

        var session = create(resumed);
        _sessions[session.SessionId] = session;
        await Subscribers.RaiseAsync(OnConversationsChanged, SubscriberLimit, _time);
        return session;
    }

    // D33: only an id the registered store's id type can parse is stored, the library never invents ids
    private ConversationStore? StoreOf(string? conversationId) =>
        conversationId is not null && _store?.Accepts(conversationId) is true ? _store : null;

    private async Task StoreAsync(
        ConversationStore store,
        TrackedConversationSession tracked,
        ConversationSession completed)
    {
        var storeLock = _storeLocks[(tracked.ConversationId!.GetHashCode() & int.MaxValue) % _storeLocks.Length];
        await storeLock.WaitAsync();

        try
        {
            await AppendAsync(store, tracked, completed);
        }
        finally
        {
            storeLock.Release();
        }
    }

    // D18: read at the end, so overlapping first sessions both land; a conversation deleted meanwhile stays deleted
    private static async Task AppendAsync(
        ConversationStore store,
        TrackedConversationSession tracked,
        ConversationSession completed)
    {
        var id = tracked.ConversationId!;
        var latest = await store.LoadAsync(id, CancellationToken.None);
        if (latest is null && tracked.Resumed) return;

        var conversation = latest is null
            ? NewConversation(tracked, completed)
            : latest with
            {
                Sessions = [.. latest.Sessions, completed],
                LastActivity = completed.FinishedAt
            };

        await store.SaveAsync(id, conversation, CancellationToken.None);
    }

    private static StoredConversation NewConversation(
        TrackedConversationSession tracked,
        ConversationSession completed) =>
        new(
            ExecutionContext: tracked.ExecutionContext,
            Sessions: [completed],
            LastActivity: completed.FinishedAt,
            CreatedAt: completed.StartedAt);
}
