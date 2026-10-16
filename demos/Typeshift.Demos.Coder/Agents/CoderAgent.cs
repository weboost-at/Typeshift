using Typeshift.Demos.Coder.Hosting;
using Typeshift.Demos.Providers;
using Typeshift.Toolsets;

namespace Typeshift.Demos.Coder.Agents;

internal partial class CoderAgent : Agent
{
    public CoderAgent()
    {
        WithDescription("Changes the code of a workspace: plans first, edits, then runs the tests.");

        WithChatOptions()
            .WithModel(ProviderCatalog.Default)
            .WithTemperature(0.2f)
            .Configurable(o => o.ModelId)
            .Configurable(o => o.Instructions);

        UseToolset<TodoToolset>();
        UseToolset<FileSystemToolset>();
        UseToolset<ShellToolset>();

        UseToolset<WebToolset>()
            .Include(t => t.WebFetch);

        UseAgent<ExplorerAgent>()
            .InBackground();

        UseContext<WorkspaceContext>();

        // Plan first; the limits suit a local model with a 32K context, and the host may raise them per workspace
        WithRunOptions()
            .WithMode(AgentMode.Plan)
            .WithMaxToolIterations(120)
            .WithMaxInputTokens(24_000)
            .WithTargetInputTokens(12_000)
            .WithSummaryModel(ProviderCatalog.Default)
            .Configurable(r => r.Mode)
            .Configurable(r => r.MaxToolIterations)
            .Configurable(r => r.MaxInputTokens);

        ConfigureRunCommand()
            .WithShell("/bin/sh")
            .WithTimeout(TimeSpan.FromMinutes(2));

        ConfigureReadFile()
            .WithMaxLines(400);
    }

    protected override string GetInstructions() =>
        """
        You are a careful coding agent working in the user's workspace. Keep a to-do list with write_todos, read a
        file before you edit it, make small edits with edit_file, and run the tests with run_command after every
        change. Ask explorer_agent when you need an overview of the workspace; it answers later as a message. When
        the task is done, say what you changed in two sentences.
        """;
}
