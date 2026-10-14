using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Approval.Support;

// The decisions a test scripts for the rules, and what the rules saw
public sealed class RuleScript
{
    public List<Func<FunctionCallContent, ToolDecision>> Decisions { get; } = [];

    public List<(string Tool, AgentMode Mode)> Seen { get; } = [];
}

public sealed class ScriptedRule(RuleScript script) : IToolApprovalRule
{
    public ValueTask<ToolDecision> DecideAsync(FunctionCallContent call, AgentRun run, CancellationToken ct)
    {
        script.Seen.Add((call.Name, run.Mode));

        return ValueTask.FromResult(script.Decisions.Count > 0 ? script.Decisions[0](call) : ToolDecision.Abstain);
    }
}

// A second rule, for decisions that meet
public sealed class SecondScriptedRule(RuleScript script) : IToolApprovalRule
{
    public ValueTask<ToolDecision> DecideAsync(FunctionCallContent call, AgentRun run, CancellationToken ct) =>
        ValueTask.FromResult(script.Decisions.Count > 1 ? script.Decisions[1](call) : ToolDecision.Abstain);
}
