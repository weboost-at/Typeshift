using Typeshift.Json;

namespace Typeshift.Agents;

// What every agent runtime needs; the client provider is the developer's and optional
internal sealed record AgentRuntimeServices(
    IServiceProvider Services,
    AgentJson Json,
    IChatClientProvider? ChatClients);
