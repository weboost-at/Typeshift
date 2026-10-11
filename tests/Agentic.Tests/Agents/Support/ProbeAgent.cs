using Agentic.Agents;
using Agentic.Tests.Toolsets.Support;

namespace Agentic.Tests.Agents.Support;

public partial class ProbeAgent : AgentDefinition
{
    public ProbeAgent()
    {
        WithUniqueName("probe_agent");
        WithDescription("Probes the settings its tools run with.");

        UseToolset<ProbeToolset>();
    }
}
