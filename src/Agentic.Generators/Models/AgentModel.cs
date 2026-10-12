namespace Agentic.Generators.Models;

internal sealed record AgentModel(
    TypeModel Type,
    bool IsPartial,
    string? UniqueName,
    EquatableArray<ChatOptionModel> ConfigurableOptions,
    EquatableArray<ToolsetUseModel> Uses,
    EquatableArray<ToolSettingsUseModel> ToolSettings,
    EquatableArray<DiagnosticInfo> Diagnostics,
    LocationInfo? Location);

// Type is the declared type of the ChatOptions property, NullableType the same type made nullable
internal sealed record ChatOptionModel(string Name, string Type, string NullableType);

// All for UseToolset<T>(), else the selected methods; IsPublic when other assemblies can name T
internal sealed record ToolsetUseModel(
    string Toolset,
    bool IsPublic,
    bool All,
    EquatableArray<SelectedMethodModel> Methods,
    LocationInfo? Location);

internal sealed record SelectedMethodModel(string Name, LocationInfo? Location);

// WithToolSettings(new T { .. }) after UsesBefore UseToolset calls; the runtime only matches tools added before it
internal sealed record ToolSettingsUseModel(string Type, int UsesBefore, LocationInfo? Location);
