using Agentic.Agents;

namespace Agentic.Tests.Toolsets.Companies;

public partial class ResearchAgent : AgentDefinition
{
    public ResearchAgent()
    {
        WithName("research_agent");
        WithDescription("Researches companies.");

        UseToolset<CompanyToolset>();
    }
}
