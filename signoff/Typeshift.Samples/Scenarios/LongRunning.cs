using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Scenarios;

internal static class LongRunning
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var settingsId = 42;

        // begin-snippet: long-running-run
        var coding = scope.ServiceProvider.GetRequiredService<AgentHost<CodingAgent>>();
        var conversationId = 12;

        await foreach (var update in coding.RunStreamingAsync("Move the solution to .NET 10.", settingsId, conversationId))
            Console.Write(update.Text);
        // end-snippet

        // begin-snippet: resume
        var resumed = await coding.ResumeAsync(conversationId, settingsId);
        // end-snippet
    }
}
