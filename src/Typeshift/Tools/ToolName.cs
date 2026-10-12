using System.Text.Json.Serialization;

namespace Typeshift.Tools;

/// <summary>
/// The lower_snake_case name a tool is offered to the model under, unique across everything a host can see.
/// </summary>
/// <param name="Value">The name.</param>
[JsonConverter(typeof(ToolNameJsonConverter))]
public sealed record ToolName(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
}
