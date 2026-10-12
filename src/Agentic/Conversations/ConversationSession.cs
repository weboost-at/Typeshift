using Microsoft.Extensions.AI;

namespace Agentic.Conversations;

/// <summary>
/// One model exchange of a conversation: the new input plus every message the model produced.
/// </summary>
/// <param name="Messages">The input, then the messages of the model and the tools, in order.</param>
/// <param name="Model">The model the exchange ran on.</param>
/// <param name="UsageDetails">The tokens the exchange used.</param>
/// <param name="FinishReason">Why the model stopped, or null when the exchange failed or was cut short.</param>
/// <param name="StartedAt">When the exchange started.</param>
/// <param name="FinishedAt">When the exchange finished.</param>
public sealed record ConversationSession(
    IReadOnlyList<ChatMessage> Messages,
    string Model,
    UsageDetails UsageDetails,
    ChatFinishReason? FinishReason,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt);
