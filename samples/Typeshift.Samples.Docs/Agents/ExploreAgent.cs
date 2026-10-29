using Typeshift.Samples.Workspace;

namespace Typeshift.Samples.Agents;

// begin-snippet: sub-agent
internal partial class ExploreAgent : Agent
{
    public ExploreAgent()
    {
        WithDescription("Explores the workspace and answers one question about it, citing files and lines.");

        WithChatOptions()
            .WithModel("openai", "gpt-5-mini");

        UseToolset<WorkspaceToolset>()
            .Include(t => t.ReadFile)
            .Include(t => t.ListFiles)
            .Include(t => t.SearchFiles);
    }

    protected override string GetInstructions() =>
        "Answer the question from the files. Read only what you need; quote the lines you rely on.";
}
// end-snippet
