namespace Agentic.Generators.Models;

// The assembly's own toolsets and agents; Name is the generated module class in Agentic.Generated
internal sealed record ModuleModel(
    string Name,
    string AssemblyName,
    EquatableArray<ToolsetSummary> Toolsets,
    EquatableArray<AgentSummary> Agents);
