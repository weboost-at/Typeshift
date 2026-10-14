namespace Typeshift.Generators.Models;

// The registrations of one assembly; type names are global::-qualified
internal sealed record ModuleModel(
    string Name,
    string AssemblyName,
    EquatableArray<string> Toolsets,
    EquatableArray<string> Agents,
    EquatableArray<string> JsonContexts);

// The modules and toolsets of the referenced assemblies compiled with the generator
internal sealed record ReferenceModel(EquatableArray<string> Modules, EquatableArray<ToolsetSummary> Toolsets);

// One services.AddTypeshift() call: the [InterceptsLocation] attribute that reroutes it
internal sealed record InterceptModel(string Attribute);
