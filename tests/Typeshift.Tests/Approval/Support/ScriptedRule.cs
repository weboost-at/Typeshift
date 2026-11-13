using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Approval.Support;

// The verdicts a test scripts for the rules, and what the rules saw
public sealed class RuleScript
{
    public List<Func<FunctionCallContent, RuleVerdict>> Verdicts { get; } = [];

    public List<(string Tool, AgentMode Mode)> Seen { get; } = [];
}

public sealed class ScriptedRule(RuleScript script) : IToolApprovalRule
{
    public ValueTask<RuleVerdict> DecideAsync(FunctionCallContent call, AgentRun run, CancellationToken ct)
    {
        script.Seen.Add((call.Name, run.Mode));

        return ValueTask.FromResult(script.Verdicts.Count > 0 ? script.Verdicts[0](call) : RuleVerdict.Abstain);
    }
}

// A second rule, for verdicts that meet
public sealed class SecondScriptedRule(RuleScript script) : IToolApprovalRule
{
    public ValueTask<RuleVerdict> DecideAsync(FunctionCallContent call, AgentRun run, CancellationToken ct) =>
        ValueTask.FromResult(script.Verdicts.Count > 1 ? script.Verdicts[1](call) : RuleVerdict.Abstain);
}
