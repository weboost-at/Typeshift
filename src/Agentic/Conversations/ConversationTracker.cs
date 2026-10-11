using System.Collections.Concurrent;
using Agentic.Persistence;
using Microsoft.Extensions.AI;

namespace Agentic.Conversations;

/// <summary>
/// Tracks the sessions of every conversation while they run and stores them when they end.
/// </summary>
public sealed class ConversationTracker
{
    internal static readonly TimeSpan SubscriberLimit = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<Guid, TrackedConversationSession> _sessions = [];
    private readonly IConversationStore? _store;
    private readonly TimeProvider _time;

    internal ConversationTracker(IConversationStore? store, TimeProvider time)
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

        if (_store is not null && tracked.Resumed)
            await _store.AppendAsync(tracked.ConversationId, completed, CancellationToken.None);
        else if (_store is not null)
            await _store.CreateAsync(NewConversation(tracked, completed), CancellationToken.None);

        await Subscribers.RaiseAsync(OnConversationsChanged, SubscriberLimit, _time);
    }

    private async Task<TSession> TrackAsync<TSession>(TSession session) where TSession : TrackedConversationSession
    {
        _sessions[session.SessionId] = session;
        await Subscribers.RaiseAsync(OnConversationsChanged, SubscriberLimit, _time);
        return session;
    }

    // An id that is not stored starts a new conversation under that id
    private async Task<(Guid Id, StoredConversation? Resumed)> ResolveConversationAsync(
        ConversationStart start,
        CancellationToken ct)
    {
        if (start.ConversationId is not { } id) return (Guid.CreateVersion7(_time.GetUtcNow()), null);

        var stored = _store is null ? null : await _store.LoadAsync(id, ct);
        return (id, stored);
    }

    private static StoredConversation NewConversation(
        TrackedConversationSession tracked,
        ConversationSession completed) =>
        new(
            Id: tracked.ConversationId,
            ExecutionContext: tracked.ExecutionContext,
            Sessions: [completed],
            LastActivity: completed.FinishedAt,
            CreatedAt: completed.StartedAt);
}
