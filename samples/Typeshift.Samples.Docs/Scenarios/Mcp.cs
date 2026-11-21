using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Scenarios;

// The two ways to an MCP server: the support agent's server, whose tools the app's provider lists beside the
// toolsets, and the status agent's hosted server, which the model provider connects itself
internal static class Mcp
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();
        var status = scope.ServiceProvider.GetRequiredService<AgentHost<StatusAgent>>();

        var offered = await support.ResolveOptionsAsync();
        if (!offered.Tools!.Any(tool => tool.Name == "service_status"))
            throw new InvalidOperationException("Expected the MCP server's tool beside the toolsets.");

        var hosted = await status.ResolveOptionsAsync();
        if (hosted.Tools is not [HostedMcpServerTool { ServerName: "status_page" }])
            throw new InvalidOperationException("Expected the hosted server, and no tool of the app, in the options.");

        var answer = await status.RunAsync("Is the status page green?");
        if (answer.Text.Length == 0) throw new InvalidOperationException("Expected the status agent to answer.");
    }
}
