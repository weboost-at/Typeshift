using Microsoft.Extensions.AI;

namespace Typeshift;

// D81: a tool that requires approval, as the tool loop sees it: the tool itself, without the mark the loop would
// pause on. The gate below the loop gives each of its calls a verdict, and the loop's invoker runs none without one
internal sealed class GatedFunction(AIFunction inner) : DelegatingAIFunction(inner)
{
    // The MCP server that offers the tool, which its decision and its standing approval name
    public string? Server { get; } = inner.GetService<McpServerFunction>()?.Server;

    public override object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(ApprovalRequiredAIFunction) ? null : base.GetService(serviceType, serviceKey);

    public static AITool Wrap(AITool tool) =>
        tool.GetService<ApprovalRequiredAIFunction>() is { } gated ? new GatedFunction(gated) : tool;

    // A call the gate cleared runs; any other is answered. A tool loop the gate does not see, as a provider's client
    // wrapped in UseFunctionInvocation() runs one below it, would otherwise run the tool unasked
    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken) =>
        IsCleared(FunctionInvokingChatClient.CurrentContext)
            ? base.InvokeCoreAsync(arguments, cancellationToken)
            : new(DecisionResults.NotApproved);

    private static bool IsCleared(FunctionInvocationContext? call) =>
        call?.Options is ResolvedChatOptions { ConversationRun: { } run }
        && run.VerdictOf(call.CallContent.CallId) is { Answer: null };
}
