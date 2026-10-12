using Agentic.Agents;
using Microsoft.Extensions.AI;

namespace Agentic.Samples.Companies;

internal partial class ResearchAgent : AgentDefinition
{
    public ResearchAgent()
    {
        WithName("research_agent");
        WithDescription("Researches the companies of a city.");

        var options = new ChatOptions
        {
            Instructions = "Find companies with the find_companies tool and list them.",
            Temperature = 0.2f
        };

        WithChatOptions(options, picker => picker
            .Configurable(o => o.Temperature)
            .Configurable(o => o.Instructions));

        UseToolset<CompanyToolset>(tools => tools.FindCompanies());

        WithToolSettings(new FindCompaniesSettings { Limit = 3 });
    }
}
