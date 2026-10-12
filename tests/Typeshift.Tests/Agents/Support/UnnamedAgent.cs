using Typeshift.Agents;
using Typeshift.Tests.Toolsets.Support;

namespace Typeshift.Tests.Agents.Support;

public partial class UnnamedAgent : AgentDefinition
{
    public UnnamedAgent() => UseToolset<ProbeToolset>();
}
