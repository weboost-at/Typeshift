namespace Typeshift.Generators.Models;

// One AddTool chain; Method is the method's escaped name, Target the global::-qualified type that declares it, Result
// the typeof() name of the value it returns, awaited, or null when it returns none
internal sealed record ToolModel(
    string Name,
    string Method,
    string Target,
    string? Description,
    bool IsReadOnly,
    bool RequiresApproval,
    SettingsModel? Settings,
    EquatableArray<ParameterModel> Parameters,
    ReturnKind Return,
    string? Result);
