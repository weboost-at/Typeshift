namespace Agentic.Generators.Models;

internal enum ParameterKind
{
    Argument,
    Services,
    CancellationToken,
    Arguments
}

// Type keeps the nullable annotation for binding, TypeOf drops it for typeof(); DefaultValue is a C# expression
internal sealed record ParameterModel(
    string Name,
    ParameterKind Kind,
    string Type,
    string TypeOf,
    bool IsNullable,
    bool HasDefaultValue,
    string? DefaultValue,
    string? Description);
