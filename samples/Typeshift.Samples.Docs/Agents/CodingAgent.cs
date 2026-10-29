using Typeshift.Samples.Workspace;

namespace Typeshift.Samples.Agents;

// begin-snippet: coding-agent
internal partial class CodingAgent : Agent
{
    public CodingAgent()
    {
        WithChatOptions()
            .WithModel("anthropic", "claude-sonnet-5-5");

        UseToolset<WorkspaceToolset>();

        // begin-snippet: background-sub-agent
        UseAgent<ExploreAgent>()
            .InBackground();
        // end-snippet

        // begin-snippet: long-running-options
        WithRunOptions()
            .WithMode(AgentMode.Plan)
            .WithMaxToolIterations(500)
            .WithMaxInputTokens(150_000)
            .WithSummaryModel("claude-haiku-5-5")
            .Configurable(r => r.Mode)
            .Configurable(r => r.MaxToolIterations)
            .Configurable(r => r.SummaryModel);
        // end-snippet
    }

    protected override string GetInstructions() =>
        """
        Work in the customer's workspace. Read before you change anything, and run the tests after every change.
        """;
}
// end-snippet
