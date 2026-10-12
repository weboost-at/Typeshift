namespace Typeshift.Generators.Models;

internal sealed record ToolsetModel(
    TypeModel Type,
    bool IsPartial,
    EquatableArray<ToolModel> Tools,
    EquatableArray<DiagnosticInfo> Diagnostics);
