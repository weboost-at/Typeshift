namespace Agentic.Generators.Models;

// An agent resolved against every visible toolset: its tools in declaration order, and what did not resolve
internal sealed record AgentOutput(
    AgentModel Agent,
    EquatableArray<ResolvedTool> Tools,
    EquatableArray<DiagnosticInfo> Diagnostics);

// Without the tool's location, so a toolset edit that moves code leaves the agent's output cached
internal sealed record ResolvedTool(string Toolset, bool IsPublic, string Name, SettingsModel? Settings)
{
    public bool HasOverrides => Settings?.HasConfigurable is true;
}
