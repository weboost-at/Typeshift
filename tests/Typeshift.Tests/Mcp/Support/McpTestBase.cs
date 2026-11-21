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

// The tests name plan mode in a run's settings, to see which of the servers' tools it offers
#pragma warning disable TSH037
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
#pragma warning restore TSH037

// A sub-agent whose only tools are those of a server that requires approval
public partial class McpHelperAgent : Agent
{
    public McpHelperAgent()
    {
        WithDescription("Deploys what the lead asks for.");

        WithChatOptions().WithModel("scripted", "mcp-helper-model");

        UseMcpServer("deploy").RequiresApproval();
    }
}

// Closes tickets itself, behind approval, and asks the helper
public partial class McpLeadAgent : Agent
{
    public McpLeadAgent()
    {
        WithChatOptions().WithModel("scripted", "mcp-lead-model");

        UseToolset<GuardedToolset>().Include(t => t.CloseTicket);

        UseAgent<McpHelperAgent>();
    }
}

// Closes tickets behind approval and hands off to an agent of a server's tools
public partial class McpFrontAgent : Agent
{
    public McpFrontAgent()
    {
        WithChatOptions().WithModel("scripted", "mcp-front-model");

        UseToolset<GuardedToolset>().Include(t => t.CloseTicket);

        UseHandoff<McpBackAgent>();
    }
}

public partial class McpBackAgent : Agent
{
    public McpBackAgent()
    {
        WithDescription("Deploys.");

        WithChatOptions().WithModel("scripted", "mcp-back-model");

        UseMcpServer("deploy").RequiresApproval();
    }
}
