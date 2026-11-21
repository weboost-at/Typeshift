using Typeshift.Samples.Quickstart;

namespace Typeshift.Samples.Agents;

// begin-snippet: agent-as-tool
internal partial class ReportAgent : Agent
{
    public ReportAgent()
    {
        WithInstructions("Write a market report. Ask research_agent for the companies of each city you cover.");

        WithChatOptions()
            .WithProvider("anthropic")
            .WithModel("claude-sonnet-5-5");

        UseAgent<ResearchAgent>();
    }
}
// end-snippet
