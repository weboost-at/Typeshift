using Agentic.Agents;
using Agentic.Tests.Toolsets.Companies;

namespace Agentic.Tests.Agents.Support;

public partial class ExcludingAgent : AgentDefinition
{
    public ExcludingAgent() => UseToolset<CompanyToolset>().Exclude(t => t.CreateCompanyProfile);
}
