namespace Typeshift;

/// <summary>
/// Thrown when a conversation is resumed that has nothing to continue: its last run ended, and no decision your app
/// answered or withdrew waits to be carried out; or retried, and its last run did not fail either. Run it with input
/// instead.
/// </summary>
/// <param name="message">The message, naming the conversation.</param>
/// <param name="conversation">The conversation's id.</param>
public sealed class NothingToResumeException(string message, object conversation)
    : InvalidOperationException(message)
{
    /// <summary>
    /// The conversation's id, as <c>WithConversationId</c> took it; for a call on the raw path, the text of
    /// <c>ChatOptions.ConversationId</c>.
    /// </summary>
    public object Conversation { get; } = conversation;

    internal static NothingToResumeException For(StoreId conversation) =>
        new(
            $"Conversation {conversation} has no interrupted run to resume and no answered decisions to carry out: "
            + "run it with input instead.",
            conversation.Value);

    // D50: a retry continues what a resume does, and a run that failed
    internal static NothingToResumeException ForRetry(StoreId conversation) =>
        new(
            $"Conversation {conversation} has no failed run to retry: its last run answered or waits for your app. "
            + "Run it with input, or answer its decisions, instead.",
            conversation.Value);
}
