namespace Typeshift.Tools;

/// <summary>
/// A model-facing parameter of a generated tool, as its schema needs it.
/// </summary>
/// <param name="Name">The parameter's name in the schema.</param>
/// <param name="Type">The parameter's type.</param>
public sealed record ToolParameter(string Name, Type Type)
{
    /// <summary>
    /// The parameter's <c>&lt;param&gt;</c> documentation.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether the parameter is optional.
    /// </summary>
    public bool HasDefaultValue { get; init; }

    /// <summary>
    /// The default of an optional parameter, boxed as its own type.
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Whether a reference-type parameter is annotated nullable.
    /// </summary>
    public bool IsNullable { get; init; }
}
