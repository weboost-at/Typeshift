using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Scenarios;

internal static class Middleware
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();

        var blocked = await support.RunAsync("What is my password?");
        if (blocked.Text != "I can't help with passwords.") throw new InvalidOperationException("Expected the block.");
    }
}
