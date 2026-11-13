namespace Typeshift;

/// <summary>
/// Thrown by a run that must ask your app and has no conversation to keep the decision in. Run with a conversation
/// id, decide the calls with an approval rule, or run in auto mode.
/// </summary>
/// <param name="message">The message, naming the tools.</param>
/// <param name="tools">The tools whose calls needed a decision.</param>
public sealed class DecisionUnavailableException(string message, IReadOnlyList<string> tools)
    : InvalidOperationException(message)
{
    /// <summary>
    /// The names of the tools whose calls needed a decision.
    /// </summary>
    public IReadOnlyList<string> Tools { get; } = tools;

    internal static DecisionUnavailableException For(IReadOnlyList<string> tools) =>
        new(
            $"The model called {string.Join(", ", tools)}, which need a decision of your app, and a run without a "
            + "conversation cannot ask for one: run with a conversation id, approve the calls with a rule, or run in "
            + "auto mode.",
            tools);
}
