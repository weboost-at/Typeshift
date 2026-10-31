namespace Typeshift;

/// <summary>
/// Thrown by a run whose input answers an approval request that is no longer open, before the run stores anything or
/// calls the model. Only the requests the conversation's last run left without an answer are open.
/// </summary>
/// <param name="message">The message, naming the conversation and each request.</param>
/// <param name="requests">Why each answered request is closed, by its id.</param>
public sealed class ApprovalClosedException(string message, IReadOnlyDictionary<string, ApprovalClosedReason> requests)
    : InvalidOperationException(message)
{
    /// <summary>
    /// Why each request the input answered is closed, by the request's id.
    /// </summary>
    public IReadOnlyDictionary<string, ApprovalClosedReason> Requests { get; } = requests;

    internal static ApprovalClosedException For(
        object? conversation,
        IReadOnlyDictionary<string, ApprovalClosedReason> requests)
    {
        var named = string.Join(", ", requests.Select(request => $"{request.Key} ({Why(request.Value)})"));

        var message = conversation is null
            ? $"A run without a conversation has no approval requests to answer: {named}. Answer them in a run of "
                + "the conversation that asked."
            : $"Conversation {conversation} has no open approval request for the answers to {named}. Only the "
                + "requests its last run left in ApprovalRequests can be answered.";

        return new ApprovalClosedException(message, requests);
    }

    private static string Why(ApprovalClosedReason reason) =>
        reason switch
        {
            ApprovalClosedReason.Answered => "answered already",
            ApprovalClosedReason.PassedBy => "the model went on without it",
            _ => "no such request"
        };
}

/// <summary>
/// Why an approval request is no longer open.
/// </summary>
public enum ApprovalClosedReason
{
    /// <summary>
    /// An earlier run of the conversation answered it, or an approval rule did.
    /// </summary>
    Answered,

    /// <summary>
    /// The model went on without the call: a later run of the conversation, or steering input that the request's own
    /// run answered.
    /// </summary>
    PassedBy,

    /// <summary>
    /// The conversation holds no request with this id since its last summary.
    /// </summary>
    Unknown
}
