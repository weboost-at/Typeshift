using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Typeshift.Generated;

/// <summary>
/// Lets <c>System.Text.Json</c> write and read settings and conversation entries in their stored shape. It refuses
/// settings JSON with a member the settings do not have or a value that does not read, which a store's read takes for
/// missing.
/// </summary>
/// <typeparam name="T">The data's type.</typeparam>
// D44: an ASP.NET endpoint that binds Settings reads the JSON a store keeps, and a body that would lose a value
// answers 400
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class StorableJsonConverter<T> : JsonConverter<T> where T : class, IJsonStorable<T>
{
    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);

        if (!T.TryReadJson(document.RootElement, out var value))
            throw new JsonException($"{typeof(T).Name} must be a JSON object.");

        T.CheckJson(document.RootElement, "");
        return value;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        value.WriteJson(writer);
}
