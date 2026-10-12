namespace Typeshift.Generators.Models;

// What the compilation's references contribute, read from their stamps only
internal sealed record ReferenceModel(
    EquatableArray<ModuleReference> Modules,
    EquatableArray<string> UngeneratedAssemblies);

internal sealed record ModuleReference(
    string Module,
    EquatableArray<ToolsetSummary> Toolsets,
    EquatableArray<string> Agents);
