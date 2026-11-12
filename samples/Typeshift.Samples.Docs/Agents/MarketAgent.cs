using Typeshift.Samples.Quickstart;

namespace Typeshift.Samples.Agents;

// begin-snippet: agent-as-tool-result
internal partial class MarketAgent : Agent
{
    public MarketAgent()
    {
        WithChatOptions()
            .WithModel("anthropic", "claude-sonnet-5-5");

        UseAgent<ResearchAgent>()
            .WithResult<CityCompanies>();
    }

    protected override string GetInstructions() =>
        "Compare the markets of the cities you are given. Ask research_agent for each city's companies.";
}

internal sealed record CityCompanies(string City, IReadOnlyList<string> Companies);
// end-snippet
