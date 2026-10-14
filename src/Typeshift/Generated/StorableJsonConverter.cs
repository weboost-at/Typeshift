using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Typeshift.Generated;

/// <summary>
/// Lets <c>System.Text.Json</c> write and read generated settings in their stored shape.
/// </summary>
/// <typeparam name="T">The settings' type.</typeparam>
// D44: an ASP.NET endpoint that binds Settings reads the JSON a store keeps
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class StorableJsonConverter<T> : JsonConverter<T> where T : class, IJsonStorable<T>
{
    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);

        return T.TryReadJson(document.RootElement, out var value)
            ? value
            : throw new JsonException($"{typeof(T).Name} must be a JSON object.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        value.WriteJson(writer);
}
