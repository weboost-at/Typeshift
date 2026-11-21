using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Carries a decision a run waits for, in the run's response or in its last streaming update. It exists in memory
/// only: it is never stored with the conversation and never sent to a provider. Read the decisions with
/// <c>response.Decisions</c>, and serialize a <see cref="Typeshift.Decision"/> rather than a whole response, since
/// Microsoft.Extensions.AI's JSON contract does not know this content.
/// </summary>
// D81: derived from the general request for input, so a consumer of Microsoft.Extensions.AI's types sees that the
// run waits; its RequestId is the decision's id
public sealed class DecisionRequestContent : InputRequestContent
{
    /// <summary>
    /// Wraps a decision.
    /// </summary>
    /// <param name="decision">The decision.</param>
    public DecisionRequestContent(Decision decision)
        : base(decision.Id) =>
        Decision = decision;

    /// <summary>
    /// The decision the run waits for.
    /// </summary>
    public Decision Decision { get; }
}
