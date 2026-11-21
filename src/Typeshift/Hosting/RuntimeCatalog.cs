using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D83: what a module registers of an agent for the catalog: its kind, and its agent and a host in a scope of its own,
// each closed over the agent's type where the generated code registered it, so the runtime makes no type at run time
internal sealed record AgentRegistration(
    Type Type,
    string Kind,
    Func<IServiceProvider, Agent> Agent,
    Func<AsyncServiceScope, IAgentHost> Host);

// D83: what a module registers of a toolset for the catalog, its tools as the generator read them
internal sealed record ToolsetRegistration(
    Type Type,
    Func<IServiceProvider, Toolset> Toolset,
    IReadOnlyList<ToolInfo> Tools);

// D83: the agents and toolsets of the app's modules, in the order the modules registered them, read once from the
// root's services: the agents' names and descriptions and the toolsets' display names are what their constructors
// recorded
internal sealed class RuntimeCatalog
{
    private readonly IReadOnlyList<AgentRegistration> _agents;

    public RuntimeCatalog(IServiceProvider services)
    {
        _agents = [.. services.GetServices<AgentRegistration>()];

        Agents =
        [
            .. _agents.Select(registration =>
            {
                var agent = registration.Agent(services);
                return new AgentInfo(registration.Type, agent.Name, registration.Kind, agent.Description)
                {
                    SwitchableTools = agent.GeneratedDeclaration.SwitchableTools
                };
            })
        ];

        Toolsets =
        [
            .. services.GetServices<ToolsetRegistration>().Select(registration => new ToolsetInfo(
                registration.Type,
                registration.Toolset(services).DisplayName,
                registration.Tools))
        ];
    }

    public IReadOnlyList<AgentInfo> Agents { get; }

    public IReadOnlyList<ToolsetInfo> Toolsets { get; }

    public AgentRegistration? Find(string kind) =>
        _agents.FirstOrDefault(registration => registration.Kind == kind);

    public AgentRegistration? Find(Type agent) =>
        _agents.FirstOrDefault(registration => registration.Type == agent);
}
