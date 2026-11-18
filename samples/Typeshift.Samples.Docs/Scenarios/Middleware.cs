using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Hosting;

namespace Typeshift.Samples.Scenarios;

// The guardrail of the middleware page refuses a question about passwords that starts a run, and drops one that is
// steered into a run, while other input passes
internal static class Middleware
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();

        var blocked = await support.RunAsync("What is my password?");
        if (blocked.Text != "I can't help with passwords.") throw new InvalidOperationException("Expected the block.");

        // The guardrail reads the input alone, so it needs none of the run it would be shown
        IRunMiddleware guardrail = new BlockedTopics();
        var steered = new ChatMessage(ChatRole.User, "And my password?");
        var other = new ChatMessage(ChatRole.User, "Send me the invoice too.");

        if (await guardrail.OnSteeringAsync(null!, steered, CancellationToken.None) is not null)
            throw new InvalidOperationException("Expected the guardrail to drop steered input about passwords.");

        if (await guardrail.OnSteeringAsync(null!, other, CancellationToken.None) != other)
            throw new InvalidOperationException("Expected other steered input to pass the guardrail.");
    }
}
