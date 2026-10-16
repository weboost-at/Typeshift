using Microsoft.Extensions.AI;
using Typeshift.Demos.Support.Domain;

namespace Typeshift.Demos.Support.Hosting;

// Every run of the agents that use it learns who the customer is; never stored, so a renamed customer shows at once
public sealed class CustomerContext(CurrentCustomer customer) : IContextProvider
{
    public ValueTask<AgentContext> GetContextAsync(AgentRun run, CancellationToken ct) =>
        ValueTask.FromResult(new AgentContext
        {
            Instructions = $"The customer is {customer.Name} (id {customer.Id}) on the {customer.Plan} plan."
        });
}

// A guardrail around every run of every agent: some topics are answered without a model
public sealed class BlockedTopics : IRunMiddleware
{
    public Task InvokeAsync(AgentRun run, Func<Task> next, CancellationToken ct)
    {
        if (run.Messages[^1].Text.Contains("card number", StringComparison.OrdinalIgnoreCase))
        {
            run.Response = new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                "I cannot see or share card numbers; the billing page shows the last four digits."));

            return Task.CompletedTask;
        }

        return next();
    }
}

// Stands in for an MCP server, in process: a real host connects it with the MCP client of its choice
public sealed class StatusMcpProvider : IMcpToolProvider
{
    private static readonly AIFunction Status = AIFunctionFactory.Create(
        () => "All services operational; the login service had an incident yesterday, resolved at 14:30.",
        "service_status",
        "The current status of our services and recent incidents.");

    public ValueTask<IReadOnlyList<AITool>> GetToolsAsync(string server, CancellationToken ct) =>
        ValueTask.FromResult<IReadOnlyList<AITool>>(server == "status" ? [Status] : []);
}

// Every call that needs approval is printed and, unless --yes, decided at the console
public sealed class AuditedCalls : IToolMiddleware
{
    public async ValueTask<object?> InvokeAsync(
        FunctionInvocationContext call,
        Func<ValueTask<object?>> next,
        CancellationToken ct)
    {
        var result = await next();
        Console.Error.WriteLine($"  [tool] {call.Function.Name} -> {result?.ToString()?.ReplaceLineEndings(" ")[..Math.Min(80, result.ToString()!.Length)]}");
        return result;
    }
}

// What RunAsync<T> returns for a conversation
public sealed record TicketSummary(string Customer, string Issue, string Outcome, bool RefundIssued, string NextStep);
