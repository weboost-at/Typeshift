using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Typeshift;

// D81: the tool loop's function invoker: the verdict the gate gave the call first, then the tool middleware (D30),
// then the tool. It fails closed: a tool that requires approval never runs without a verdict
internal sealed class ToolGate(IReadOnlyList<IToolMiddleware> middleware, ILogger logger)
{
    public ValueTask<object?> InvokeAsync(FunctionInvocationContext call, CancellationToken ct)
    {
        var verdict = (call.Options as ResolvedChatOptions)?.ConversationRun?.VerdictOf(call.CallContent.CallId);

        if (verdict is { Answer: { } answer }) return new(answer);

        if (verdict is not null || !NeedsVerdict(call.Function))
            return ToolMiddleware.InvokeAsync(middleware, 0, call, ct);

        logger.LogError(
            "The call {CallId} of {Tool} reached the tool loop without a verdict of the gate, so it did not run.",
            call.CallContent.CallId,
            call.Function.Name);

        return new(DecisionResults.NotApproved);
    }

    private static bool NeedsVerdict(AIFunction function) =>
        function.GetService<GatedFunction>() is not null || function is ExitPlanModeFunction or AskUserFunction;
}

// What Typeshift settled for one call before it could run: run it, or answer it with a text in the tool's place
internal readonly record struct CallVerdict(string? Answer)
{
    public static CallVerdict Run => default;

    public static CallVerdict AnswerWith(string answer) => new(answer);
}

// What the model reads for a call that did not simply run
internal static class DecisionResults
{
    public const string Expired = "The user did not answer and sent a new message instead. This call did not run.";

    public const string Unknown =
        "This call was approved and started, and the run stopped before its result was stored. Whether it took "
        + "effect is unknown: check before you repeat it.";

    public const string NotHandedOff = "Not handed off: this step waited for the user.";

    public const string NotRun = "This call did not run: this step waited for the user.";

    public const string NotPlanning = "You are not in plan mode: go on with the task.";

    public const string NotApproved = "This call was not approved, so it did not run.";

    // D33: a sub-agent cannot ask your app
    public const string SubAgent =
        "This agent runs as a sub-agent and cannot ask for approval; no rule or standing approval allows the call.";

    private const string PlanApproved = "The user approved the plan. Carry it out now.";

    // The verdict of a call of an answered batch: your app's answer, the rule's rejection, or run it
    public static CallVerdict VerdictOf(BatchCall call) =>
        call switch
        {
            { Rejected: { } reason } => CallVerdict.AnswerWith(reason),
            { Ask: { Kind: AskKind.Plan, Answer.Approved: true } } => CallVerdict.AnswerWith(PlanApproved),
            { Ask: { Kind: AskKind.Plan, Answer: var answer } } =>
                CallVerdict.AnswerWith(WithReason("The user rejected the plan.", answer?.Reason)),
            { Ask: { Kind: AskKind.Question, Answer: var answer } } =>
                CallVerdict.AnswerWith(AskUserFunction.Result(call.Questions, answer)),
            { Ask: { Answer.Approved: true } } or { Ask: null } => CallVerdict.Run,
            { Ask.Answer: var answer } =>
                CallVerdict.AnswerWith(WithReason("The user rejected this call.", answer?.Reason))
        };

    // The results of calls that did not run, as one tool message
    public static ChatMessage Message(IEnumerable<(string CallId, string Result)> results) =>
        new(
            ChatRole.Tool,
            [.. results.Select(result => (AIContent)new FunctionResultContent(result.CallId, result.Result))]);

    private static string WithReason(string rejection, string? reason) =>
        reason is null ? rejection : $"{rejection} {reason}";
}
