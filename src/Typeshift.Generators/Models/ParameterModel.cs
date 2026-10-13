namespace Typeshift.Generators.Models;

internal enum ParameterKind
{
    Argument,
    Services,
    Settings,
    CancellationToken,
    Arguments
}

// Type keeps the nullable annotation for binding, TypeOf drops it for typeof(); DefaultValue is a C# expression,
// null without a default
internal sealed record ParameterModel(
    string Name,
    ParameterKind Kind,
    string Type,
    string TypeOf,
    bool IsNullable,
    string? DefaultValue,
    string? Description);
