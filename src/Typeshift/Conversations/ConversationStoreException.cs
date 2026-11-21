using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Thrown by a run whose conversation the store did not keep whole: a step of the run failed to append. The run went
/// on and answered, and <see cref="Response"/> holds what it answered, but later runs of the conversation lack what
/// was not stored. The store's exception is the inner exception. To have such a run return its response instead,
/// marked as incomplete, set <c>TypeshiftOptions.StoreFailure</c> to <see cref="StoreFailure.ContinueRun"/>. Also
/// thrown at once, whatever that option says, by a run that asks your app a decision the store failed to keep: the
/// decisions document or the model's response that asks failed to save, so nothing waits for an answer.
/// </summary>
/// <param name="message">The message, naming the conversation.</param>
/// <param name="conversation">The conversation's id.</param>
/// <param name="response">
/// What the run answered, or the model's response that asked decisions the store failed to keep.
/// </param>
/// <param name="innerException">
/// The store's exception: of the first append that failed, or of the save of the decisions the run asks.
/// </param>
public sealed class ConversationStoreException(
    string message,
    object conversation,
    ChatResponse response,
    Exception innerException)
    : InvalidOperationException(message, innerException)
{
    /// <summary>
    /// The conversation's id, as <c>WithConversationId</c> took it; for a call on the raw path, the text of
    /// <c>ChatOptions.ConversationId</c>.
    /// </summary>
    public object Conversation { get; } = conversation;

    /// <summary>
    /// What the run answered, as it would have returned it; a streaming run's updates as one response. Its
    /// <c>Decisions</c> are stored when it lists any, so your app can still show and answer them. For a run whose
    /// decisions the store failed to keep, the model's response that asked, whose calls did not run.
    /// </summary>
    public ChatResponse Response { get; } = response;

    internal static ConversationStoreException For(StoreId conversation, ChatResponse response, Exception inner) =>
        new(
            $"Conversation {conversation} is missing a step of this run, since the store failed to append it, so the "
            + "run fails although it answered: the answer is in Response, and the store's exception is the inner "
            + "exception. Set TypeshiftOptions.StoreFailure to ContinueRun to have such a run return its response, "
            + "marked as incomplete.",
            conversation.Value,
            response,
            inner);

    // D46: a decision your app could not answer would be no decision, so the run that asks it fails at once
    internal static ConversationStoreException Undecided(
        StoreId conversation,
        ChatResponse response,
        Exception inner) =>
        new(
            $"The store failed to keep the decisions this run of conversation {conversation} asks your app, or "
            + "the model's response that asks them, so the run fails at once, whatever TypeshiftOptions.StoreFailure "
            + "says, and nothing waits for an answer: the model's response is in Response, and the store's exception "
            + "is the inner exception.",
            conversation.Value,
            response,
            inner);
}

/// <summary>
/// What a run does when the store fails to append a step of its conversation.
/// </summary>
public enum StoreFailure
{
    /// <summary>
    /// The run goes on to its end and then throws a <see cref="ConversationStoreException"/> that holds its answer.
    /// </summary>
    FailRun,

    /// <summary>
    /// The run returns its answer, and <c>response.ConversationIncomplete</c> says that the conversation lacks a step.
    /// </summary>
    ContinueRun
}
