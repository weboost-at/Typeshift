using Microsoft.Extensions.AI;

namespace Agentic.Conversations;

/// <summary>
/// One model exchange of a conversation: the new input plus every message the model produced.
/// </summary>
public sealed record ConversationSession(
    IReadOnlyList<ChatMessage> Messages,
    string Model,
    UsageDetails UsageDetails,
    ChatFinishReason? FinishReason,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt);
