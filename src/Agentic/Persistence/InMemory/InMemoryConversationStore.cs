using System.Collections.Concurrent;
using Agentic.Conversations;

namespace Agentic.Persistence.InMemory;

/// <summary>
/// Keeps conversations in memory, for tests, samples and single-process hosts.
/// </summary>
public sealed class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<Guid, StoredConversation> _conversations = [];

    /// <inheritdoc />
    public Task<StoredConversation?> LoadAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_conversations.GetValueOrDefault(id));

    /// <inheritdoc />
    public Task CreateAsync(StoredConversation conversation, CancellationToken ct)
    {
        _conversations[conversation.Id] = conversation;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task AppendAsync(Guid id, ConversationSession session, CancellationToken ct)
    {
        _conversations.AddOrUpdate(
            id,
            _ => throw new InvalidOperationException($"No conversation {id} is stored."),
            (_, stored) => stored with
            {
                Sessions = [.. stored.Sessions, session],
                LastActivity = session.FinishedAt
            });

        return Task.CompletedTask;
    }
}
