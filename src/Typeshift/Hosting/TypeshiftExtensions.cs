using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// The members Typeshift adds to Microsoft.Extensions.AI's types and to its own: what a run's response, its updates
/// and its messages carry, the run of a tool call, and the kind of failure an HTTP status tells.
/// </summary>
public static class TypeshiftExtensions
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

        // D73: the id names the conversation as text, which a host continues only typed as its store keeps it
        /// <summary>
        /// The id of the conversation the run stored, typed as your conversation store keeps it, such as the
        /// <see cref="Guid"/> of a conversation that <c>PersistAllConversations</c> started:
        /// <c>support.WithConversationId(response.GetConversationId&lt;Guid&gt;())</c> continues it. It parses
        /// <c>ConversationId</c>, the text Microsoft.Extensions.AI carries, with the invariant culture.
        /// </summary>
        /// <typeparam name="TId">The id type of your conversation store.</typeparam>
        /// <returns>The conversation's id.</returns>
        /// <exception cref="InvalidOperationException">The run stored no conversation.</exception>
        /// <exception cref="FormatException">The id is not a <typeparamref name="TId"/>.</exception>
        public TId GetConversationId<TId>() where TId : IParsable<TId> =>
            TId.Parse(
                response.ConversationId
                ?? throw new InvalidOperationException(
                    "The run stored no conversation: give the host a conversation id, or turn on "
                    + "PersistAllConversations, to keep its runs."),
                System.Globalization.CultureInfo.InvariantCulture);
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

        /// <summary>
        /// Whether the update is the last one a watcher of the conversation receives of a run, after the run's other
        /// updates: the run answered, waits for your app, failed, or stopped. A run that failed or stopped holds the
        /// error its stored end holds, as an <see cref="ErrorContent"/>; one that answered has the
        /// <see cref="ChatResponseUpdate.FinishReason"/> of its last model call. The run's own stream has no such
        /// update: it ends.
        /// More in the guide:
        /// <see href="https://weboost-at.github.io/Typeshift/guide/conversations#live-updates">Live updates</see>.
        /// </summary>
        public bool EndsRun => update.AdditionalProperties?.ContainsKey(RunEnded) is true;
    }

    extension(ChatMessage message)
    {
        /// <summary>
        /// Whether a background sub-agent's answer joined the run with this message, or Typeshift's note that the
        /// sub-agent failed or was lost. The message is in the user's role, the one a run takes between model calls,
        /// so the stored conversation holds it among the user's: its <see cref="ChatMessage.AuthorName"/> names the
        /// sub-agent, and its first line tells the model it is no message from the user. Show it as the sub-agent's.
        /// More in the guide:
        /// <see href="https://weboost-at.github.io/Typeshift/guide/multi-agent#in-the-background">Background</see>.
        /// </summary>
        public bool IsBackgroundAnswer =>
            message.AdditionalProperties?.ContainsKey(ConversationRun.BackgroundCall) is true;
    }

    // D30: the call's options are the run's, which hold the run, so a tool middleware reads it without a parameter of
    // its own and IToolMiddleware keeps Microsoft.Extensions.AI's context alone
    extension(FunctionInvocationContext call)
    {
        /// <summary>
        /// The run the call belongs to, as run middleware sees it: its agent, its services, its ids and its options.
        /// A tool middleware reads it to act for some agents only, or to name the customer a tool ran for.
        /// More in the guide:
        /// <see href="https://weboost-at.github.io/Typeshift/guide/middleware#wrap-every-tool-call">Wrap every tool
        /// call</see>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The call is not one of a run's tool loop, such as one your own <see cref="FunctionInvokingChatClient"/>
        /// made.
        /// </exception>
        public AgentRun Run =>
            (call.Options as ResolvedChatOptions)?.AgentRun
            ?? throw new InvalidOperationException(
                $"The call of '{call.Function.Name}' does not belong to a run of an agent: only the tool calls of a "
                + "run, as a tool middleware sees them, carry it.");
    }

    extension(ModelFailure)
    {
        // D89: the table of statuses Typeshift reads from an HttpRequestException, for your app's classification of a
        // client library's exception that holds a status
        /// <summary>
        /// The kind of failure an HTTP status tells: 429 <see cref="ModelFailure.RateLimit"/>, 408 and 504
        /// <see cref="ModelFailure.Timeout"/>, 401 and 403 <see cref="ModelFailure.Auth"/>, 400, 404, 413 and 422
        /// <see cref="ModelFailure.Rejected"/>, any other of 500 or more <see cref="ModelFailure.Unavailable"/>, and
        /// any other status <see cref="ModelFailure.Unknown"/>. Use it in
        /// <see cref="TypeshiftOptions.ClassifyModelFailure"/> for an exception that holds the status, as
        /// <c>ModelFailure.FromStatus(failed.Status)</c> for the OpenAI client's <c>ClientResultException</c>.
        /// </summary>
        /// <param name="status">The HTTP status the provider answered with.</param>
        /// <returns>The kind.</returns>
        [Experimental(ExperimentalIds.ModelPolicy, UrlFormat = ExperimentalIds.ModelPolicyUrl)]
        public static ModelFailure FromStatus(int status) =>
            status switch
            {
                429 => ModelFailure.RateLimit,
                408 or 504 => ModelFailure.Timeout,
                401 or 403 => ModelFailure.Auth,
                400 or 404 or 413 or 422 => ModelFailure.Rejected,
                >= 500 => ModelFailure.Unavailable,
                _ => ModelFailure.Unknown
            };
    }

    // D46: the mark a run that was not stored whole puts on what it returns
    internal const string Incomplete = "typeshift.conversation_incomplete";

    // D54: the mark of a run's last update to its watchers
    internal const string RunEnded = "typeshift.run_ended";

    private static IEnumerable<Decision> DecisionsIn(IEnumerable<AIContent> contents) =>
        contents.OfType<DecisionRequestContent>().Select(request => request.Decision);

    private static bool IsIncomplete(AdditionalPropertiesDictionary? properties) =>
        properties?.TryGetValue(Incomplete, out var incomplete) is true && incomplete is true;
}
