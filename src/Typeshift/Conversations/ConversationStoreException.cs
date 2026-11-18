using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Thrown by a run whose conversation the store did not keep whole: a step of the run failed to append. The run went
/// on and answered, and <see cref="Response"/> holds what it answered, but later runs of the conversation lack what
/// was not stored. The store's exception is the inner exception. To have such a run return its response instead,
/// marked as incomplete, set <c>TypeshiftOptions.StoreFailure</c> to <see cref="StoreFailure.ContinueRun"/>.
/// </summary>
/// <param name="message">The message, naming the conversation.</param>
/// <param name="conversation">The conversation's id.</param>
/// <param name="response">What the run answered.</param>
/// <param name="inner">The exception of the first append that failed.</param>
public sealed class ConversationStoreException(
    string message,
    object conversation,
    ChatResponse response,
    Exception inner)
    : InvalidOperationException(message, inner)
{
    /// <summary>
    /// The conversation's id, as <c>WithConversationId</c> took it; for a call on the raw path, the text of
    /// <c>ChatOptions.ConversationId</c>.
    /// </summary>
    public object Conversation { get; } = conversation;

    /// <summary>
    /// What the run answered, as it would have returned it; a streaming run's updates as one response. Its
    /// <c>Decisions</c> are stored when it lists any, so your app can still show and answer them.
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
