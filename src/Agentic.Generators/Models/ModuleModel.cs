namespace Agentic.Generators.Models;

// Without locations, so code that only moves leaves the module cached
internal sealed record ModuleModel(
    string Name,
    string AssemblyName,
    EquatableArray<string> Toolsets,
    EquatableArray<string> Agents);
