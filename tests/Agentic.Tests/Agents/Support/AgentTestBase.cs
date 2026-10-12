using Agentic.Agents;
using Agentic.Persistence;
using Agentic.Persistence.InMemory;
using Agentic.Settings;
using Agentic.Tests.Testing.Fakes;
using Agentic.Tests.Toolsets.Companies;
using Agentic.Tests.Toolsets.Support;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Tests.Agents.Support;

// A host with an in-memory store under Guid ids, running the probe agent
public abstract class AgentTestBase : HostTestBase
{
    protected AgentTestData Agent => Data.Agent;

    protected AgentRuntime<ProbeAgent> Runtime => Services.GetRequiredService<AgentRuntime<ProbeAgent>>();

    protected ToolDefaults ToolDefaults => Services.GetRequiredService<ToolDefaults>();

    protected IAgenticStore<Guid> Store => Services.GetRequiredService<IAgenticStore<Guid>>();

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddAgenticStore<InMemoryAgenticStore<Guid>, Guid>();
        services.AddSingleton<IChatClientProvider>(new ScriptedChatClientProvider(ChatClient));
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
