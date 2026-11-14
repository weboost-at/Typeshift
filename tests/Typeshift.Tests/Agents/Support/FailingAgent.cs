namespace Typeshift.Tests.Agents.Support;

// Each tool throws with the text it was given, so a test follows an exception's message to where it ends up
public partial class FailingToolset : Toolset
{
    public FailingToolset()
    {
        AddTool(Crash)
            .WithDescription("Fails as a database driver would, with a message nobody wrote for a model.");

        AddTool(Refuse)
            .WithDescription("Fails with a hint its author wrote for the model.");
    }

    /// <param name="message">The exception's message.</param>
    public static string Crash(string message) => throw new InvalidOperationException(message);

    /// <param name="hint">What the model is to read.</param>
    public static string Refuse(string hint) => throw new ToolException(hint);
}

// One failed round of tool calls is allowed, the second in a row ends the run
public partial class FailingAgent : Agent
{
    public FailingAgent()
    {
        WithChatOptions()
            .WithModel(ProbeAgent.ModelProvider, ProbeAgent.Model);

        WithRunOptions()
            .WithMaxConsecutiveToolErrors(1);

        UseToolset<FailingToolset>();
    }
}
