namespace Typeshift;

/// <summary>
/// Thrown when an answer names a decision that is not open. Nothing of the call that brought the answer is recorded,
/// so your app can tell its user that the prompt is out of date and record the rest again.
/// </summary>
/// <param name="message">The message, naming the conversation and each decision.</param>
/// <param name="decisions">Why each answered decision is closed, by its id.</param>
public sealed class DecisionClosedException(
    string message,
    IReadOnlyDictionary<string, DecisionClosedReason> decisions)
    : InvalidOperationException(message)
{
    /// <summary>
    /// Why each decision the answers named is closed, by the decision's id.
    /// </summary>
    public IReadOnlyDictionary<string, DecisionClosedReason> Decisions { get; } = decisions;

    internal static DecisionClosedException For(
        StoreId conversation,
        IReadOnlyDictionary<string, DecisionClosedReason> decisions)
    {
        var named = string.Join(", ", decisions.Select(decision => $"{decision.Key} ({Why(decision.Value)})"));

        return new DecisionClosedException(
            $"Conversation {conversation} has no open decision for the answers to {named}. Only the decisions "
            + "GetDecisionsAsync lists as open, which the last response carried in Decisions, can be answered.",
            decisions);
    }

    private static string Why(DecisionClosedReason reason) =>
        reason switch
        {
            DecisionClosedReason.Answered => "answered already",
            DecisionClosedReason.Expired => "the user sent something else",
            _ => "no such decision"
        };
}

/// <summary>
/// Why a decision is no longer open.
/// </summary>
public enum DecisionClosedReason
{
    /// <summary>
    /// Another answer stands, or a run is carrying the answers out or has done so.
    /// </summary>
    Answered,

    /// <summary>
    /// New input passed the decision by: the user sent a message instead of answering, and the model read that the
    /// call did not run.
    /// </summary>
    Expired,

    /// <summary>
    /// The conversation holds no decision with this id among those it asked last.
    /// </summary>
    Unknown
}
