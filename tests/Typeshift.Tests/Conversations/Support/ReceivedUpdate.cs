using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Conversations.Support;

// An update as the consumer received it, with what its session had published by then
public record ReceivedUpdate(ChatResponseUpdate Update, IReadOnlyList<ChatResponseUpdate> Published);
