namespace Agentic.Generators.Models;

// One AddTool chain; Method is the method name, Target the global::-qualified type that declares it
internal sealed record ToolModel(
    string Name,
    string Method,
    string Target,
    string? Description,
    SettingsModel? Settings,
    EquatableArray<ParameterModel> Parameters,
    ReturnKind Return,
    LocationInfo? Location);
