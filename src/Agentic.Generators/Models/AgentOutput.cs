namespace Agentic.Generators.Models;

// An agent resolved against every visible toolset: its tools in declaration order, and what did not resolve
internal sealed record AgentOutput(
    AgentModel Agent,
    EquatableArray<ResolvedTool> Tools,
    EquatableArray<DiagnosticInfo> Diagnostics);

internal sealed record ResolvedTool(string Toolset, ToolSummary Tool);
