using Agentic.Agents;

namespace Agentic.Conversations;

/// <summary>
/// A conversation as stored: every finished session, in order.
/// </summary>
public sealed record StoredConversation(
    Guid Id,
    ChatExecutionContext ExecutionContext,
    IReadOnlyList<ConversationSession> Sessions,
    DateTimeOffset LastActivity,
    DateTimeOffset CreatedAt);
