using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agentic.Tools;

internal sealed class ToolNameJsonConverter : JsonConverter<ToolName>
{
    public override ToolName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, ToolName value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);

    // Stored tool settings are keyed by tool name
    public override ToolName ReadAsPropertyName(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        new(reader.GetString()!);

    public override void WriteAsPropertyName(Utf8JsonWriter writer, ToolName value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Value);
}
