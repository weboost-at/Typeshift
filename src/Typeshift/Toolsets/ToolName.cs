namespace Typeshift;

/// <summary>
/// A tool's name, lower_snake_case.
/// </summary>
/// <param name="Value">The name.</param>
public readonly record struct ToolName(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
}
