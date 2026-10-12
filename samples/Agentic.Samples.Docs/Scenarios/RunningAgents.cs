using Agentic.Agents;
using Agentic.Samples.Docs.Agents;
using Agentic.Samples.Docs.Tickets;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Samples.Docs.Scenarios;

internal static class RunningAgents
{
    public static async Task RunAsync()
    {
        await using var services = Host.Create();

        // begin-snippet: run-from-code
        var support = services.GetRequiredService<AgentRuntime<SupportAgent>>();

        var options = support.ResolveChatOptions();
        var client = await support.CreateChatClientAsync(options);
        var response = await client.GetResponseAsync("Where is my refund?", options);
        // end-snippet

        Expect.That(response.Text == "Answered by fast-model.", "the code's model answers");

        var triage = services.GetRequiredService<AgentRuntime<TriageAgent>>().ResolveChatOptions();

        Expect.That(triage.Tools!.Single().Name == "search_tickets", "excluded tools are not offered");

        // begin-snippet: run-settings
        var run = new SupportAgent.Settings
        {
            Model = "reasoning-model",
            Temperature = 0,
            SearchTickets = new TicketToolset.SearchTicketsOverrides { IncludeClosed = true }
        };

        var runOptions = support.ResolveChatOptions(run);
        var runClient = await support.CreateChatClientAsync(runOptions);
        // end-snippet

        Expect.That(runOptions.Temperature == 0, "run settings override the code's chat options");

        // begin-snippet: streaming
        await foreach (var update in runClient.GetStreamingResponseAsync("Where is my refund?", runOptions))
            Console.Write(update.Text);
        // end-snippet

        Console.WriteLine();
    }
}
