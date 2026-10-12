using Agentic.Conversations;

namespace Agentic.Persistence;

/// <summary>
/// Stores tracked conversations; optional, register an implementation as a singleton to keep them.
/// </summary>
public interface IConversationStore
{
    /// <summary>
    /// The stored conversation, or null.
    /// </summary>
    Task<StoredConversation?> LoadAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Stores a new conversation.
    /// </summary>
    Task CreateAsync(StoredConversation conversation, CancellationToken ct);

    /// <summary>
    /// Appends a finished session to a stored conversation and moves its last activity to the session's end; a
    /// conversation deleted meanwhile stays deleted.
    /// </summary>
    Task AppendAsync(Guid id, ConversationSession session, CancellationToken ct);
}
