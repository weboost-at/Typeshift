namespace Typeshift;

/// <summary>
/// A tool of a toolset as the runtime lists it, with what a tool picker shows of it.
/// More in the guide: <see href="https://weboost-at.github.io/Typeshift/guide/registration#the-catalog">The
/// catalog</see>.
/// </summary>
public sealed record ToolInfo
{
    /// <summary>
    /// The tool's name, lower_snake_case, as the model and its toolset's <c>ToolNames</c> hold it.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// What the tool does, as the model reads it: its <c>WithDescription</c>, or the summary or <c>[Description]</c> of
    /// its method; null when it has none.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether the tool only reads, marked with <c>ReadOnly()</c>.
    /// </summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// Whether its calls wait for approval, marked with <c>RequiresApproval()</c>.
    /// </summary>
    public bool RequiresApproval { get; init; }
}
