using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Conversations;

/// <summary>
/// Adds conversation tracking to a chat client pipeline.
/// </summary>
public static class ConversationTrackingExtensions
{
    extension(ChatClientBuilder builder)
    {
        /// <summary>
        /// Tracks every model exchange of the clients built, unary and streaming, as sessions of a conversation.
        /// </summary>
        public ChatClientBuilder UseConversationTracking(string model) =>
            builder.Use((inner, services) =>
                new TrackedChatClient(inner, services.GetRequiredService<ConversationTracker>(), model));
    }
}
