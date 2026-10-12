using Agentic.Agents;
using Microsoft.Extensions.AI;

namespace Agentic.Conversations;

// What a tracked call starts with; a conversation id resumes that conversation, or starts one under it
internal sealed record ConversationStart(
    string? ConversationId,
    string Model,
    ChatExecutionContext ExecutionContext,
    IReadOnlyList<ChatMessage> Input);
