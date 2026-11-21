using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Typeshift.Samples.Workspace;

namespace Typeshift.Samples.Hosting;

// begin-snippet: tool-middleware
internal sealed class AuditToolCalls(ILogger<AuditToolCalls> logger) : IToolMiddleware
{
    public async ValueTask<object?> InvokeAsync(
        FunctionInvocationContext call,
        Func<ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        var run = call.Run;
        int? customer = run.TryGetSettingsId<int>(out var id) ? id : null;

        logger.LogInformation(
            "{Agent} calls {Tool} for customer {Customer}",
            run.Agent.Name,
            call.Function.Name,
            customer);

        if (call.Function.Name == WorkspaceToolset.ToolNames.RunCommand)
            logger.LogWarning("{Agent} runs {Command}", run.Agent.Name, call.Arguments["command"]);

        return await next();
    }
}
// end-snippet
