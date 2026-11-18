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

/// <summary>
/// What runs return beyond Microsoft.Extensions.AI.
/// </summary>
public static class ChatResponseExtensions
{
    extension(ChatResponse response)
    {
        /// <summary>
        /// The decisions the run waits for: answer each with <c>AnswerAsync</c>, by its id. Empty for a run that
        /// answered.
        /// </summary>
        public IReadOnlyList<Decision> Decisions =>
            [.. response.Messages.SelectMany(message => DecisionsIn(message.Contents))];

        /// <summary>
        /// Whether the store failed to append a step of the run, which later runs of the conversation then lack. Only
        /// a run under <see cref="StoreFailure.ContinueRun"/> returns such a response; by default the run throws a
        /// <see cref="ConversationStoreException"/>.
        /// </summary>
        public bool ConversationIncomplete => IsIncomplete(response.AdditionalProperties);
    }

    extension(ChatResponseUpdate update)
    {
        /// <summary>
        /// The decisions the run waits for, which a streaming run sends in its last update, after it stored its
        /// end. Empty in every other update.
        /// </summary>
        public IReadOnlyList<Decision> Decisions => [.. DecisionsIn(update.Contents)];

        /// <summary>
        /// Whether the store failed to append a step of the run, which a streaming run under
        /// <see cref="StoreFailure.ContinueRun"/> says in an update of its own after the others. The response the
        /// updates make has <c>ConversationIncomplete</c> set then.
        /// </summary>
        public bool ConversationIncomplete => IsIncomplete(update.AdditionalProperties);
    }

    // D46: the mark a run that was not stored whole puts on what it returns
    internal const string Incomplete = "typeshift.conversation_incomplete";

    private static IEnumerable<Decision> DecisionsIn(IEnumerable<AIContent> contents) =>
        contents.OfType<DecisionRequestContent>().Select(request => request.Decision);

    private static bool IsIncomplete(AdditionalPropertiesDictionary? properties) =>
        properties?.TryGetValue(Incomplete, out var incomplete) is true && incomplete is true;
}
