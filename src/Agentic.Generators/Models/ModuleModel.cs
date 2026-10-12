namespace Agentic.Generators.Models;

// The assembly's own toolsets and agents, without locations so moved code leaves the module cached; Name is the
// generated module class in Agentic.Generated
internal sealed record ModuleModel(
    string Name,
    string AssemblyName,
    EquatableArray<string> Toolsets,
    EquatableArray<ModuleAgentModel> Agents);

internal sealed record ModuleAgentModel(string Type, string UniqueName);
