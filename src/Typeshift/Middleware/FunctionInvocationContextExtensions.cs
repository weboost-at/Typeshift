using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// What a tool call of a run carries beyond Microsoft.Extensions.AI.
/// </summary>
// D30: the call's options are the run's, which hold the run, so a tool middleware reads it without a parameter of
// its own and IToolMiddleware keeps Microsoft.Extensions.AI's context alone
public static class FunctionInvocationContextExtensions
{
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
}
