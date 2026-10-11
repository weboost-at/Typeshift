using Agentic.Json;
using Agentic.Persistence;
using Agentic.Settings;

namespace Agentic.Agents;

// What every agent runtime needs; the stores and the client provider are the developer's and optional
internal sealed record AgentRuntimeServices(
    IServiceProvider Services,
    AgentJson Json,
    ToolDefaultsManager ToolDefaults,
    TimeProvider Time,
    IAgentConfigurationStore? Configurations,
    IChatClientProvider? ChatClients);
