using Microsoft.Extensions.AI;

namespace Agentic.Agents;

/// <summary>
/// Reads and sets the execution context in a chat's additional properties.
/// </summary>
public static class ChatExecutionContextExtensions
{
    extension(AdditionalPropertiesDictionary properties)
    {
        /// <summary>
        /// The context the chat runs in, or <see cref="ChatExecutionContext.Unknown"/>.
        /// </summary>
        public ChatExecutionContext GetExecutionContext() =>
            properties.TryGetValue<ChatExecutionContext>(ChatExecutionContext.Key, out var context)
                ? context
                : ChatExecutionContext.Unknown;

        /// <summary>
        /// Sets the context the chat runs in.
        /// </summary>
        public AdditionalPropertiesDictionary WithExecutionContext(ChatExecutionContext context)
        {
            properties[ChatExecutionContext.Key] = context;
            return properties;
        }
    }
}
