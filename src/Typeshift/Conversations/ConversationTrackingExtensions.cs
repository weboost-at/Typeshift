using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Conversations;

/// <summary>
/// Adds conversation tracking to a chat client pipeline.
/// </summary>
public static class ConversationTrackingExtensions
{
    extension(ChatClientBuilder builder)
    {
        /// <summary>
        /// Tracks every model exchange of the clients built, unary and streaming, as sessions of a conversation.
        /// <see cref="ChatOptions.ConversationId"/> names the conversation; the provider never receives it.
        /// </summary>
        /// <param name="model">The model the client runs on, recorded with every session.</param>
        /// <returns>The same builder, for chaining.</returns>
        public ChatClientBuilder UseConversationTracking(string model) =>
            builder.Use((inner, services) =>
                new TrackedChatClient(inner, services.GetRequiredService<ConversationTracker>(), model));
    }
}
