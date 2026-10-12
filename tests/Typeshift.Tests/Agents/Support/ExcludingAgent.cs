using Typeshift.Agents;
using Typeshift.Tests.Toolsets.Companies;

namespace Typeshift.Tests.Agents.Support;

public partial class ExcludingAgent : AgentDefinition
{
    public ExcludingAgent() => UseToolset<CompanyToolset>().Exclude(t => t.CreateCompanyProfile);
}
