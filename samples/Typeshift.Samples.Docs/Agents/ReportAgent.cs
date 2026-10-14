using Typeshift.Samples.Quickstart;

namespace Typeshift.Samples.Agents;

// begin-snippet: agent-as-tool
internal partial class ReportAgent : Agent
{
    public ReportAgent()
    {
        WithChatOptions().WithModel("reasoning-model");

        UseAgent<ResearchAgent>();
    }

    protected override string GetInstructions() =>
        "Write a market report. Ask research_agent for the companies of each city you cover.";
}
// end-snippet
