namespace Typeshift.Generators.Models;

// The registrations of one assembly; type names are global::-qualified. ExperimentalIds are those of its toolsets and
// agents
internal sealed record ModuleModel(
    string Name,
    string AssemblyName,
    EquatableArray<ModuleToolset> Toolsets,
    EquatableArray<ModuleAgent> Agents,
    EquatableArray<string> JsonContexts,
    EquatableArray<string> SchemaAnnotations,
    EquatableArray<string> ExperimentalIds);

// D83: a toolset and what the runtime's catalog lists of each of its tools, for a tool picker
internal sealed record ModuleToolset(string Type, EquatableArray<ModuleTool> Tools);

internal sealed record ModuleTool(string Name, string? Description, bool IsReadOnly, bool RequiresApproval);

// D33, D34, D39: an agent, the kind of its stored Settings, and what the agents of other assemblies check
internal sealed record ModuleAgent(
    string Type,
    string Kind,
    string Name,
    bool Described,
    EquatableArray<string> ApprovalTools,
    EquatableArray<string> Tools,
    EquatableArray<string> OwnTools,
    bool AsksUser);

// The modules and toolsets of the referenced assemblies compiled with the generator
internal sealed record ReferenceModel(EquatableArray<string> Modules, EquatableArray<ToolsetSummary> Toolsets);

// One services.AddTypeshift() or TypeshiftRuntime.Create() call: the [InterceptsLocation] attribute that reroutes it
internal sealed record InterceptModel(string Attribute, bool IsRuntime);
