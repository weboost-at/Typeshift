using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Approval.Support;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Mcp.Support;

// The agent tests' host, with servers whose tools a test scripts by name
public abstract class McpTestBase : AgentTestBase
{
    protected ScriptedServers Servers { get; } = new();

    protected ToolCalls ToolCalls { get; } = [];

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddTypeshift().WithMcpToolProvider<ScriptedMcpToolProvider>();
        services.AddSingleton(Servers);
        services.AddSingleton(ToolCalls);
    }

    protected AIFunction Tool(string name) =>
        AIFunctionFactory.Create(() => ToolCalls.Add(name), name, $"The {name} tool.");
}

public sealed class ScriptedServers : Dictionary<string, List<AITool>>;

public sealed class ScriptedMcpToolProvider(ScriptedServers servers) : IMcpToolProvider
{
    public ValueTask<IReadOnlyList<AITool>> GetToolsAsync(string server, CancellationToken ct) =>
        ValueTask.FromResult<IReadOnlyList<AITool>>(servers.GetValueOrDefault(server) ?? []);
}

public partial class McpAgent : Agent
{
    public McpAgent()
    {
        WithChatOptions().WithModel("scripted", "mcp-model");

        WithRunOptions().Configurable(r => r.Mode);

        UseToolset<GuardedToolset>().Include(t => t.Note);

        UseMcpServer("status").ReadOnly();

        UseMcpServer("deploy").RequiresApproval();
    }
}
