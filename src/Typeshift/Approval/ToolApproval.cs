using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Decides tool calls before the host is asked: approves what the host trusts, rejects what it never allows (the guard
/// of auto mode). Registered with <c>WithApprovalRule&lt;T&gt;()</c>, resolved from the run's services.
/// </summary>
public interface IToolApprovalRule
{
    /// <summary>
    /// Decides a call of a tool that requires approval. A rejection wins over every approval.
    /// </summary>
    /// <param name="call">The call, with the arguments the model sent.</param>
    /// <param name="run">The run, with its mode.</param>
    /// <param name="ct">Cancels the decision.</param>
    /// <returns>The decision.</returns>
    ValueTask<ToolDecision> DecideAsync(FunctionCallContent call, AgentRun run, CancellationToken ct);
}

/// <summary>
/// A rule's decision on a tool call.
/// </summary>
public readonly record struct ToolDecision
{
    /// <summary>
    /// No opinion: the mode decides, asking the host in <see cref="AgentMode.Default"/>, running the call in
    /// <see cref="AgentMode.Auto"/>.
    /// </summary>
    public static ToolDecision Abstain => default;

    /// <summary>
    /// The call runs.
    /// </summary>
    public static ToolDecision Approve => new() { Approved = true };

    /// <summary>
    /// Whether the call runs.
    /// </summary>
    public bool Approved { get; private init; }

    /// <summary>
    /// Why the call is rejected, which the model receives; null unless it is.
    /// </summary>
    public string? Rejection { get; private init; }

    /// <summary>
    /// The call is rejected in every mode; the model receives the reason.
    /// </summary>
    /// <param name="reason">Why, for the model.</param>
    /// <returns>The decision.</returns>
    public static ToolDecision Reject(string reason) => new() { Rejection = reason };
}

/// <summary>
/// What runs return beyond Microsoft.Extensions.AI.
/// </summary>
public static class ChatResponseExtensions
{
    // The marks a response carries in its additional properties, stored with the conversation
    internal const string ForConversation = "typeshift.approve_for_conversation";

    internal const string PlanMode = "typeshift.plan_mode";

    extension(ChatResponse response)
    {
        /// <summary>
        /// The tool calls waiting for the host's approval; answer each with <c>CreateResponse(approved)</c> in the
        /// next run of the conversation.
        /// </summary>
        public IReadOnlyList<ToolApprovalRequestContent> ApprovalRequests =>
            [.. response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>()];
    }

    extension(ToolApprovalRequestContent request)
    {
        /// <summary>
        /// Approves this call and every later call of the same tool in the conversation. The approval is stored with
        /// the conversation, so it holds across runs and processes.
        /// </summary>
        /// <returns>The response to send in the conversation's next run.</returns>
        public ToolApprovalResponseContent ApproveForConversation() =>
            Mark(request.CreateResponse(approved: true), ForConversation, true);

        /// <summary>
        /// Approves a plan that <c>exit_plan_mode</c> presented and continues in <paramref name="mode"/>.
        /// </summary>
        /// <param name="mode"><see cref="AgentMode.Default"/> or <see cref="AgentMode.Auto"/>.</param>
        /// <returns>The response to send in the conversation's next run.</returns>
        public ToolApprovalResponseContent ApprovePlan(AgentMode mode) =>
            Mark(request.CreateResponse(approved: true), PlanMode, mode.ToString());
    }

    private static ToolApprovalResponseContent Mark(ToolApprovalResponseContent response, string key, object value)
    {
        (response.AdditionalProperties ??= [])[key] = value;
        return response;
    }
}
