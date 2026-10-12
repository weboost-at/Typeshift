using Typeshift.Agents;
using Typeshift.Tests.Toolsets.Support;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Agents.Support;

public partial class HostedToolAgent : AgentDefinition
{
    public HostedToolAgent()
    {
        WithName("hosted_tool_agent");
        WithDescription("Searches the web through its provider, then probes.");

        WithChatOptions().WithTools(new HostedWebSearchTool());

        UseToolset<ProbeToolset>();
    }
}
