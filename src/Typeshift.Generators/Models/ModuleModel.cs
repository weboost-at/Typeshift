namespace Typeshift.Generators.Models;

// The registrations of one assembly; type names are global::-qualified
internal sealed record ModuleModel(
    string Name,
    string AssemblyName,
    string IdType,
    string ProviderIdType,
    EquatableArray<string> Toolsets,
    EquatableArray<ModuleAgent> Agents,
    EquatableArray<string> JsonContexts,
    EquatableArray<string> SchemaAnnotations);

// D39: an agent and the kind its stored Settings go by
internal sealed record ModuleAgent(string Type, string Kind);

// The modules and toolsets of the referenced assemblies compiled with the generator
internal sealed record ReferenceModel(EquatableArray<string> Modules, EquatableArray<ToolsetSummary> Toolsets);

// One services.AddTypeshift() call: the [InterceptsLocation] attribute that reroutes it
internal sealed record InterceptModel(string Attribute);
