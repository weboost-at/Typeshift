using Agentic.Agents;
using Agentic.Catalogs;
using Agentic.Persistence;
using Agentic.Persistence.InMemory;
using Agentic.Settings;
using Agentic.Tests.Toolsets.Companies;
using Agentic.Tests.Toolsets.Support;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Tests.Agents.Support;

// A host with in-memory stores, running the probe agent
public abstract class AgentTestBase : HostTestBase
{
    protected AgentTestData Agent => Data.Agent;

    protected AgentRuntime<ProbeAgent> Runtime => Services.GetRequiredService<AgentRuntime<ProbeAgent>>();

    protected ToolDefaultsManager ToolDefaults => Services.GetRequiredService<ToolDefaultsManager>();

    protected AgentCatalog Catalog => Services.GetRequiredService<AgentCatalog>();

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IAgentConfigurationStore, InMemoryAgentConfigurationStore>();
        services.AddSingleton<IToolDefaultsStore, InMemoryToolDefaultsStore>();
        services.AddSingleton<IAgentDefinitionStore, InMemoryAgentDefinitionStore>();
    }

    protected async Task<ProbeSettings> ProbeAsync(ChatOptions options)
    {
        var result = await Tool(options, "probe").InvokeAsync(Arguments(("query", Data.Tool.Query)), Ct);
        return ReadResult<ProbeResult>(result).Settings;
    }

    protected async Task<FindCompaniesSettings> FindCompaniesAsync(ChatOptions options)
    {
        var result = await Tool(options, "find_companies").InvokeAsync(Arguments(("query", Data.Tool.Query)), Ct);
        return ReadResult<FindCompaniesSettings>(result);
    }

    private static AIFunction Tool(ChatOptions options, string name) =>
        options.Tools!.OfType<AIFunction>().Single(tool => tool.Name == name);
}
