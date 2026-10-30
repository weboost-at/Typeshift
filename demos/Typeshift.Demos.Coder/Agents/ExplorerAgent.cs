using Typeshift.Demos.Coder.Toolsets;

namespace Typeshift.Demos.Coder.Agents;

// Reads only, so none of its calls needs approval and a background run of it never waits for the host
internal partial class ExplorerAgent : Agent
{
    public ExplorerAgent()
    {
        WithDescription("Explores the workspace and answers one question about it, citing files and lines.");

        WithChatOptions()
            .Configurable(o => o.ModelId);

        UseToolset<FileToolset>()
            .Include(t => t.ListFiles)
            .Include(t => t.SearchFiles)
            .Include(t => t.ReadFile);

        WithRunOptions()
            .WithMaxToolIterations(20);
    }

    protected override string GetInstructions() =>
        "Answer the question from the files. Read only what you need and quote the lines you rely on.";
}
