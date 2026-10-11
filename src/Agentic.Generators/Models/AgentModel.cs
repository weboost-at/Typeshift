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

// UseToolset<Toolset>() when All, else UseToolset<Toolset>(Toolset.M1, Toolset.M2)
internal sealed record ToolsetUseModel(
    string Toolset,
    bool All,
    EquatableArray<SelectedMethodModel> Methods,
    LocationInfo? Location);

internal sealed record SelectedMethodModel(string Name, LocationInfo? Location);

// WithToolSettings(new T { .. }) after UsesBefore UseToolset calls; the runtime only matches tools added before it
internal sealed record ToolSettingsUseModel(string Type, int UsesBefore, LocationInfo? Location);
