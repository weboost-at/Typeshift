using Agentic.Agents;
using Agentic.Tests.Toolsets.Support;
using Microsoft.Extensions.AI;

namespace Agentic.Tests.Agents.Support;

public partial class HostedToolAgent : AgentDefinition
{
    public HostedToolAgent()
    {
        WithName("hosted_tool_agent");
        WithDescription("Searches the web through its provider, then probes.");

        WithChatOptions(new ChatOptions { Tools = [new HostedWebSearchTool()] });

        UseToolset<ProbeToolset>();
    }
}
