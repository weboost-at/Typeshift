using Typeshift.Agents;

namespace Typeshift.Samples.Companies;

// begin-snippet: quickstart-agent
internal partial class ResearchAgent : AgentDefinition
{
    public ResearchAgent()
    {
        WithName("research_agent");
        WithDescription("Researches the companies of a city.");

        WithChatOptions()
            .WithTemperature(0.2f)
            .Configurable(o => o.Temperature)
            .Configurable(o => o.Instructions);

        UseToolset<CompanyToolset>().Include(t => t.FindCompanies);

        ConfigureFindCompanies()
            .WithLimit(3);
    }

    protected override string GetInstructions() =>
        """
        Find companies with the find_companies tool and list them.
        """;
}
// end-snippet
