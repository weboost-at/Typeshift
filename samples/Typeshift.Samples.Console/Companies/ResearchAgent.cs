using Typeshift.Samples.Chat;

namespace Typeshift.Samples.Companies;

internal partial class ResearchAgent : Agent
{
    public ResearchAgent()
    {
        WithDescription("Researches the companies of a city.");

        WithChatOptions()
            .WithModel(Provider.Scripted, "scripted-model")
            .WithTemperature(0.2f)
            .Configurable(o => o.Temperature);

        WithRunOptions()
            .Configurable(r => r.MaxToolIterations);

        UseToolset<CompanyToolset>();

        ConfigureFindCompanies()
            .WithLimit(3);
    }

    protected override string GetInstructions() => "Find companies with the find_companies tool and list them.";
}

// What a run asking for structured output returns
internal sealed record CompanyCount(string City, int Count);
