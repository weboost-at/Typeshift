using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Typeshift;

// D52: the tool that ends plan mode. Its call always waits for your app, and the app's answer is its result, so the
// function itself never runs
internal sealed class ExitPlanModeFunction : AIFunction
{
    public const string ToolName = "exit_plan_mode";

    public static readonly ExitPlanModeFunction Instance = new();

    public const string Instructions =
        "You are in plan mode: research the task with the read-only tools you have, then present your plan with "
        + "exit_plan_mode. Make no changes until the plan is approved; when it is rejected, revise it with the "
        + "feedback and present it again.";

    private static readonly JsonElement Schema = JsonDocument.Parse(
            """
            {
              "type": "object",
              "properties": {
                "plan": { "type": "string", "description": "The plan, in Markdown, step by step." }
              },
              "required": ["plan"]
            }
            """)
        .RootElement
        .Clone();

    private ExitPlanModeFunction()
    {
    }

    public override string Name => ToolName;

    public override string Description =>
        "Presents your plan to the user for approval, once you know what to do. The user approves it, or tells you "
        + "what to change.";

    public override JsonElement JsonSchema => Schema;

    // D81: the tool loop's invoker answers the call from the decision's record and never gets here
    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken ct) =>
        throw new InvalidOperationException("exit_plan_mode is answered by your app's decision, never invoked.");
}
