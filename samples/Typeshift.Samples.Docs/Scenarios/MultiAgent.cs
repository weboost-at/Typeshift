using System.Text.Json;
using Microsoft.Extensions.AI;
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

        await TypedAnswerAsync(scope.ServiceProvider);
    }

    // A sub-agent held to a type answers its caller with that type's JSON
    private static async Task TypedAnswerAsync(IServiceProvider services)
    {
        var market = services.GetRequiredService<AgentHost<MarketAgent>>();
        var compared = await market.RunAsync("Compare the markets of Vienna and Graz.");

        var result = compared.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Single()
            .Result;

        var companies = JsonSerializer.Deserialize<CityCompanies>($"{result}", JsonSerializerOptions.Web);
        if (companies is not { City: "Vienna", Companies.Count: > 0 })
            throw new InvalidOperationException($"Expected the research agent's answer as CityCompanies: {result}");
    }
}
