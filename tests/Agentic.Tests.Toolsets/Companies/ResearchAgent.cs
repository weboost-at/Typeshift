using Agentic.Agents;

namespace Agentic.Tests.Toolsets.Companies;

public partial class ResearchAgent : AgentDefinition
{
    public ResearchAgent()
    {
        WithUniqueName("research_agent");
        WithDescription("Researches companies.");

        UseToolset<CompanyToolset>();
    }
}
