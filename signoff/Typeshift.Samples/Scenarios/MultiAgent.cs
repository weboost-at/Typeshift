using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Scenarios;

internal static class MultiAgent
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var settingsId = 42;

        // begin-snippet: agent-as-tool-run
        var reports = scope.ServiceProvider.GetRequiredService<AgentHost<ReportAgent>>();

        var report = await reports.RunAsync("Compare the robotics scenes of Vienna and Graz.", settingsId);
        // end-snippet

        // begin-snippet: handoff-run
        var triage = scope.ServiceProvider.GetRequiredService<AgentHost<TriageAgent>>();
        var conversationId = 8;

        var first = await triage.RunAsync("I was charged twice.", settingsId, conversationId);
        var second = await triage.RunAsync("It was invoice 2024-17.", settingsId, conversationId);

        Console.WriteLine($"{second.Messages[^1].AuthorName}: {second.Text}");
        // end-snippet
    }
}
