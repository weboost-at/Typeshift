namespace Typeshift;

/// <summary>
/// Thrown by a run without a conversation whose calls would need a decision of your app, which it has nowhere to
/// keep. A run whose agent offers such a tool fails before it calls the model: a tool that requires approval while no
/// approval rule is registered and the run is not in auto mode, or the plan of plan mode. With approval rules
/// registered, it fails at a call no rule decided. Run with a conversation id, keep every conversation with
/// <c>TypeshiftOptions.PersistAllConversations</c>, or, for a tool that requires approval, decide its calls with an
/// approval rule or run in auto mode.
/// </summary>
/// <param name="message">The message, naming the tools.</param>
/// <param name="tools">The tools whose calls need a decision.</param>
public sealed class DecisionUnavailableException(string message, IReadOnlyList<string> tools)
    : InvalidOperationException(message)
{
    /// <summary>
    /// The names of the tools whose calls need a decision: those the agent offers, for a run that failed before it
    /// called the model, or those the model called.
    /// </summary>
    public IReadOnlyList<string> Tools { get; } = tools;

    internal static DecisionUnavailableException For(IReadOnlyList<string> tools) =>
        new(
            $"The model called {string.Join(", ", tools)}, which need a decision of your app, and a run without a "
            + "conversation cannot ask for one: run with a conversation id or keep every conversation, approve the "
            + "calls with a rule, or run in auto mode.",
            tools);

    // D81: before the first model call, naming the ways to run such an agent; a rule or auto mode decides only the
    // calls of a tool that requires approval
    internal static DecisionUnavailableException Before(string agent, IReadOnlyList<string> tools, bool approvals) =>
        new(
            $"The agent '{agent}' offers {string.Join(", ", tools)}, whose calls need a decision of your app, and a "
            + "run without a conversation has nowhere to keep one, so the run fails before it calls the model. Run it "
            + "with a conversation id, or keep every conversation with TypeshiftOptions.PersistAllConversations."
            + (approvals
                ? " The calls of a tool that requires approval can be decided by an approval rule, registered with "
                    + "WithApprovalRule<T>(), or by auto mode instead."
                : ""),
            tools);
}
