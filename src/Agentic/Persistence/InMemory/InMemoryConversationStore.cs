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
        while (_conversations.TryGetValue(id, out var stored))
        {
            var appended = stored with
            {
                Sessions = [.. stored.Sessions, session],
                LastActivity = session.FinishedAt
            };

            if (_conversations.TryUpdate(id, appended, stored)) break;
        }

        return Task.CompletedTask;
    }
}
