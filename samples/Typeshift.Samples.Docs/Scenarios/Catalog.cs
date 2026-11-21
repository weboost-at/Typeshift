using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Tickets;

namespace Typeshift.Samples.Scenarios;

// D83: the runtime's catalog: what an admin page lists of the agents and toolsets, and an agent run by the kind a
// request names, from a host of its own and from a scope
internal static class Catalog
{
    public static async Task RunAsync(IChatClient client)
    {
        await using var runtime = TypeshiftRuntime.Create(client);

        // begin-snippet: catalog-list
        foreach (var agent in runtime.Agents)
            Console.WriteLine($"{agent.Kind}: {agent.Description}");

        foreach (var toolset in runtime.Toolsets)
        {
            var tools = toolset.Tools.Select(tool => tool.RequiresApproval ? $"{tool.Name} (asks first)" : tool.Name);
            Console.WriteLine($"{toolset.DisplayName ?? toolset.Type.Name}: {string.Join(", ", tools)}");
        }
        // end-snippet

        var research = runtime.Agents.SingleOrDefault(agent => agent.Kind == "ResearchAgent");
        Expect(research is { Description: "Researches the companies of a city." }, "the research agent in the list");

        var tickets = runtime.Toolsets.Single(toolset => toolset.Type == typeof(TicketToolset));
        var close = tickets.Tools.Single(tool => tool.Name == TicketToolset.ToolNames.CloseTicket);
        Expect(tickets.DisplayName == "Tickets" && close.RequiresApproval, "the ticket tools with their display name");

        var kind = "ResearchAgent";
        var question = "Vienna";

        // begin-snippet: catalog-host
        await using var host = runtime.CreateHost(kind);
        var answer = await host.RunAsync(question);
        // end-snippet

        Expect(host.Agent.Name == "research_agent" && !string.IsNullOrEmpty(answer.Text), "the agent of the kind");

        await using var scope = runtime.Services.CreateAsyncScope();
        var keyed = scope.ServiceProvider.GetRequiredKeyedService<IAgentHost>(kind);
        Expect(keyed.Services == scope.ServiceProvider, "the keyed host to run in the scope it was resolved from");
    }

    private static void Expect(bool condition, string what)
    {
        if (!condition) throw new InvalidOperationException($"Expected {what}.");
    }
}
