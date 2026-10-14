using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Hosting;

// begin-snippet: approval-rule
internal sealed class SafeCommands : IToolApprovalRule
{
    private static readonly string[] Allowed = ["dotnet build", "dotnet test", "git status", "git diff"];

    public ValueTask<bool> ApprovesAsync(FunctionCallContent call, AgentRun run, CancellationToken ct) =>
        ValueTask.FromResult(
            call is { Name: "run_command", Arguments: { } arguments }
            && arguments["command"] is string command
            && command.IndexOfAny([';', '&', '|', '`', '$', '>', '<', '\n']) < 0
            && Allowed.Any(command.StartsWith));
}
// end-snippet
