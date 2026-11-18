namespace Typeshift;

/// <summary>
/// Thrown when a run starts on a conversation that has a run going on in this process, which takes the input
/// itself: hand it over with <c>ConversationTracker.TrySteer</c>, or start the run again once that run has ended.
/// Nothing of the refused run is stored.
/// </summary>
/// <param name="message">The message, naming the conversation.</param>
/// <param name="conversation">The conversation's id.</param>
public sealed class ConversationBusyException(string message, object conversation)
    : InvalidOperationException(message)
{
    /// <summary>
    /// The conversation's id, as <c>WithConversationId</c> took it; for a call on the raw path, the text of
    /// <c>ChatOptions.ConversationId</c>.
    /// </summary>
    public object Conversation { get; } = conversation;

    internal static ConversationBusyException For(StoreId conversation) =>
        new(
            $"A run of conversation {conversation} is going on in this process, and a conversation has one run at a "
            + "time: hand it the input with ConversationTracker.TrySteer, or start the run again once it has ended.",
            conversation.Value);
}
