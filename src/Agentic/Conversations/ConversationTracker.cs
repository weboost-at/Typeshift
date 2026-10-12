using System.Collections.Concurrent;
using Microsoft.Extensions.AI;

namespace Agentic.Conversations;

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

    internal async Task<TrackedConversationSession> TrackSessionAsync(ConversationStart start, CancellationToken ct)
    {
        var (id, resumed) = await ResolveConversationAsync(start, ct);
        return await TrackAsync(new TrackedConversationSession(id, start, resumed, _time));
    }

    internal async Task<RealtimeTrackedConversationSession> TrackRealtimeSessionAsync(
        ConversationStart start,
        CancellationToken ct)
    {
        var (id, resumed) = await ResolveConversationAsync(start, ct);
        return await TrackAsync(new RealtimeTrackedConversationSession(id, start, resumed, _time));
    }

    // Storing what happened is not cancelled with the call, a cancelled call is stored as well
    internal async Task EndSessionAsync(Guid sessionId, ChatResponse response)
    {
        if (!_sessions.TryRemove(sessionId, out var tracked)) return;

        var completed = await tracked.CompleteAsync(response);
        if (StoreOf(tracked.ConversationId) is { } store) await StoreAsync(store, tracked, completed);

        await Subscribers.RaiseAsync(OnConversationsChanged, SubscriberLimit, _time);
    }

    private async Task<TSession> TrackAsync<TSession>(TSession session) where TSession : TrackedConversationSession
    {
        _sessions[session.SessionId] = session;
        await Subscribers.RaiseAsync(OnConversationsChanged, SubscriberLimit, _time);
        return session;
    }

    // An id that is not stored starts a new conversation under that id
    private async Task<(string? Id, StoredConversation? Resumed)> ResolveConversationAsync(
        ConversationStart start,
        CancellationToken ct)
    {
        if (start.ConversationId is not { } id || StoreOf(id) is not { } store) return (start.ConversationId, null);

        return (id, await store.LoadAsync(id, ct));
    }

    // D33: only an id the registered store's id type can parse is stored, the library never invents ids
    private ConversationStore? StoreOf(string? conversationId) =>
        conversationId is not null && _store?.Accepts(conversationId) is true ? _store : null;

    // D18: read at the end, so overlapping first sessions both land; a conversation deleted meanwhile stays deleted
    private static async Task StoreAsync(
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
