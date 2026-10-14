using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Typeshift;

// D52: the tool that ends plan mode; it always asks the host, and runs only once the host approved the plan
internal sealed class ExitPlanModeFunction : AIFunction
{
    public const string ToolName = "exit_plan_mode";

    public static readonly AITool Instance = new ApprovalRequiredAIFunction(new ExitPlanModeFunction());

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

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken ct) =>
        new("The user approved the plan. Carry it out now.");
}
