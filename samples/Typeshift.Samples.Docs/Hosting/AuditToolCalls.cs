using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Typeshift.Samples.Hosting;

// begin-snippet: tool-middleware
internal sealed class AuditToolCalls(ICurrentCustomer customer, ILogger<AuditToolCalls> logger) : IToolMiddleware
{
    public async ValueTask<object?> InvokeAsync(
        FunctionInvocationContext call,
        Func<ValueTask<object?>> next,
        CancellationToken ct)
    {
        logger.LogInformation("Customer {Customer} calls {Tool}", customer.Id, call.Function.Name);

        return await next();
    }
}
// end-snippet
