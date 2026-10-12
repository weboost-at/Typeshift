using Microsoft.Extensions.AI;

namespace Typeshift.Agents;

/// <summary>
/// Reads and sets the execution context in a chat's additional properties.
/// </summary>
public static class ChatExecutionContextExtensions
{
    extension(AdditionalPropertiesDictionary properties)
    {
        /// <summary>
        /// Reads the context the chat runs in.
        /// </summary>
        /// <returns>The context, or <see cref="ChatExecutionContext.Unknown"/> when none is set.</returns>
        public ChatExecutionContext GetExecutionContext() =>
            properties.TryGetValue<ChatExecutionContext>(ChatExecutionContext.Key, out var context)
                ? context
                : ChatExecutionContext.Unknown;

        /// <summary>
        /// Sets the context the chat runs in.
        /// </summary>
        /// <param name="context">The context, usually the name of the agent that runs the chat.</param>
        /// <returns>The same properties, for chaining.</returns>
        public AdditionalPropertiesDictionary WithExecutionContext(ChatExecutionContext context)
        {
            properties[ChatExecutionContext.Key] = context;
            return properties;
        }
    }
}
