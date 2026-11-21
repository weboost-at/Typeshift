using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Orders;

namespace Typeshift.Samples.Scenarios;

// The calls of one response run at the same time in the run's scope, and each creates a context of its own
internal static class ConcurrentToolCalls
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using (var db = await provider.GetRequiredService<IDbContextFactory<ShopDb>>().CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
        }

        await using var scope = provider.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<AgentHost<OrderAgent>>();

        var answer = await orders.RunAsync("Where are orders 1 and 2?");

        var results = answer.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .ToList();

        if (results.Count != 2 || results.Any(result => result.Exception is not null || result.Result is null))
            throw new InvalidOperationException("Expected both calls of one response to read their order.");
    }
}
