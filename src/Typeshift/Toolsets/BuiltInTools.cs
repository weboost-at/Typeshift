namespace Typeshift;

/// <summary>
/// The names of the tools a run offers itself, which no tool of your toolsets or agents may have. An approval rule,
/// a tool middleware or a UI names them here, as it names your own tools through each toolset's generated
/// <c>ToolNames</c>.
/// More in the guide:
/// <see href="https://weboost-at.github.io/Typeshift/guide/toolsets#names-and-descriptions">Names and
/// descriptions</see>.
/// </summary>
public static class BuiltInTools
{
    /// <summary>
    /// The tool through which the model asks the user, which an agent offers with <c>UseQuestions()</c>.
    /// </summary>
    public const string AskUser = AskUserFunction.ToolName;

    /// <summary>
    /// The tool through which the model presents its plan in plan mode.
    /// </summary>
    public const string ExitPlanMode = ExitPlanModeFunction.ToolName;

    /// <summary>
    /// What the name of each tool <c>UseHandoff&lt;T&gt;()</c> offers starts with, followed by the name of the agent
    /// it hands the conversation to.
    /// </summary>
    public const string HandoffPrefix = ConversationChatClient.HandoffPrefix;
}
