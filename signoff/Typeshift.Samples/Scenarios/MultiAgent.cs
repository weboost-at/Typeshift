using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Scenarios;

internal static class MultiAgent
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var customer = 42;

        // begin-snippet: agent-as-tool-run
        var reports = scope.ServiceProvider.GetRequiredService<AgentHost<ReportAgent>>();

        var report = await reports.RunAsync("Compare the robotics scenes of Vienna and Graz.", id: customer);
        // end-snippet

        // begin-snippet: handoff-run
        var triage = scope.ServiceProvider.GetRequiredService<AgentHost<TriageAgent>>();
        var conversation = 8;

        var first = await triage.RunAsync("I was charged twice.", id: customer, conversationId: conversation);
        var second = await triage.RunAsync("It was invoice 2024-17.", id: customer, conversationId: conversation);

        Console.WriteLine($"{second.Messages[^1].AuthorName}: {second.Text}");
        // end-snippet
    }
}
