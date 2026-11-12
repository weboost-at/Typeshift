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
        var reports = scope.ServiceProvider.GetRequiredService<AgentHost<ReportAgent>>()
            .WithSettingsId(settingsId);

        var report = await reports.RunAsync("Compare the robotics scenes of Vienna and Graz.");
        // end-snippet

        if (report.Messages.Count < 3) throw new InvalidOperationException("Expected the sub-agent's call.");

        // begin-snippet: handoff-run
        var triage = scope.ServiceProvider.GetRequiredService<AgentHost<TriageAgent>>()
            .WithSettingsId(settingsId)
            .WithConversationId(21);

        var first = await triage.RunAsync("I was charged twice.");
        var second = await triage.RunAsync("It was invoice 2024-17.");

        Console.WriteLine($"{second.Messages[^1].AuthorName}: {second.Text}");
        // end-snippet

        if (first.Messages[^1].AuthorName != "billing_agent" || second.Messages[^1].AuthorName != "billing_agent")
            throw new InvalidOperationException("Expected billing to answer after the handoff.");
    }
}
