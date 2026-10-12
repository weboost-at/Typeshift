using Agentic.Agents;
using Agentic.Tests.Toolsets.Support;

namespace Agentic.Tests.Agents.Support;

public partial class UnnamedAgent : AgentDefinition
{
    public UnnamedAgent() => UseToolset<ProbeToolset>();
}
