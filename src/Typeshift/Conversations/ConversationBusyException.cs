namespace Typeshift;

/// <summary>
/// Thrown when a run starts on a conversation that has a run going on in this container, which takes the input
/// itself: hand it over with <c>ConversationTracker.TrySteer</c>, or start the run again once that run has ended.
/// Nothing of the refused run is stored. Also thrown by a run whose step the store refused because a run in another
/// container, such as another instance of your app, appended to the conversation first: that run stopped at the step,
/// what it stored before stays, and the store's <see cref="StoreConflictException"/> is the inner exception. And
/// thrown by <c>ConversationTracker.TrySteer</c> when the run going on holds as much steering input as it takes before
/// its next model call: 32 inputs, or 262,144 characters of text with the one refused. Steer again once the model took
/// them, or start a run with the input once that run has ended.
/// </summary>
/// <param name="message">The message, naming the conversation.</param>
/// <param name="conversation">The conversation's id.</param>
/// <param name="inner">The store's exception, for a run whose step the store refused; otherwise null.</param>
public sealed class ConversationBusyException(string message, object conversation, Exception? inner = null)
    : InvalidOperationException(message, inner)
{
    /// <summary>
    /// The conversation's id, as <c>WithConversationId</c> took it; for a call on the raw path, the text of
    /// <c>ChatOptions.ConversationId</c>.
    /// </summary>
    public object Conversation { get; } = conversation;

    internal static ConversationBusyException For(StoreId conversation) =>
        new(
            $"A run of conversation {conversation} is going on in this container, and a conversation has one run at a "
            + "time: hand it the input with ConversationTracker.TrySteer, or start the run again once it has ended.",
            conversation.Value);

    // D51: a run takes so much steering input before its next model call, and a client that floods it is told so
    internal static ConversationBusyException Full(StoreId conversation) =>
        new(
            $"The run of conversation {conversation} going on in this container holds as much steering input as it "
            + $"takes before its next model call, {ConversationRun.MaxSteeringInputs} inputs or "
            + $"{ConversationRun.MaxSteeringCharacters} characters of text with this one: steer it again once its "
            + "model took them, or start a run with the input once it has ended.",
            conversation.Value);

    // D50: the store refused an entry under a number another container's run had taken
    internal static ConversationBusyException Refused(StoreId conversation, StoreConflictException conflict) =>
        new(
            $"The store refused a step of this run of conversation {conversation}, because a run in another container, "
            + "such as another instance of your app, appended to the conversation after this run loaded it, and a "
            + "conversation has one run at a time. This run stopped there, and what it stored before stays: start "
            + "it again once the other run has ended.",
            conversation.Value,
            conflict);
}
