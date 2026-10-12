using System.Buffers;
using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Agentic.Persistence;

/// <summary>
/// Writes and reads the JSON of generated <see cref="IStorable{TSelf}"/> data, member by member and without
/// reflection. Generated code calls it; a null member is left out, and a missing or mistyped one reads as null.
/// </summary>
// D27: source generators do not see each other's output, so no JsonSerializerContext can cover generated settings
[EditorBrowsable(EditorBrowsableState.Never)]
public static class StorableJson
{
    /// <summary>
    /// Writes a JSON object.
    /// </summary>
    /// <param name="writeMembers">Writes the members of the object.</param>
    /// <returns>The object as JSON text.</returns>
    public static string Write(Action<Utf8JsonWriter> writeMembers)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writeMembers(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// Reads a JSON object.
    /// </summary>
    /// <typeparam name="T">The data the object holds.</typeparam>
    /// <param name="json">The JSON text.</param>
    /// <param name="readMembers">Reads the data from the object.</param>
    /// <returns>The data.</returns>
    /// <exception cref="JsonException">The text is not a JSON object.</exception>
    public static T Read<T>(string json, Func<JsonElement, T> readMembers)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind is not JsonValueKind.Object)
            throw new JsonException($"Stored data must be a JSON object, not {document.RootElement.ValueKind}.");

        return readMembers(document.RootElement);
    }

    /// <summary>
    /// Writes a string member.
    /// </summary>
    /// <param name="writer">The writer of the enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <param name="value">The value; null leaves the member out.</param>
    public static void Write(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null) writer.WriteString(name, value);
    }

    /// <summary>
    /// Writes a Boolean member.
    /// </summary>
    /// <param name="writer">The writer of the enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <param name="value">The value; null leaves the member out.</param>
    public static void Write(Utf8JsonWriter writer, string name, bool? value)
    {
        if (value is { } boolean) writer.WriteBoolean(name, boolean);
    }

    /// <summary>
    /// Writes a number member.
    /// </summary>
    /// <typeparam name="TNumber">The number type.</typeparam>
    /// <param name="writer">The writer of the enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <param name="value">The value; null leaves the member out.</param>
    public static void WriteNumber<TNumber>(Utf8JsonWriter writer, string name, TNumber? value)
        where TNumber : struct, INumber<TNumber>
    {
        if (value is not { } number) return;

        writer.WritePropertyName(name);
        writer.WriteRawValue(number.ToString(null, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Writes a <see cref="Guid"/> member as a string.
    /// </summary>
    /// <param name="writer">The writer of the enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <param name="value">The value; null leaves the member out.</param>
    public static void Write(Utf8JsonWriter writer, string name, Guid? value)
    {
        if (value is { } guid) writer.WriteString(name, guid);
    }

    /// <summary>
    /// Writes a <see cref="DateTime"/> member as an ISO 8601 string.
    /// </summary>
    /// <param name="writer">The writer of the enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <param name="value">The value; null leaves the member out.</param>
    public static void Write(Utf8JsonWriter writer, string name, DateTime? value)
    {
        if (value is { } time) writer.WriteString(name, time);
    }

    /// <summary>
    /// Writes a <see cref="DateTimeOffset"/> member as an ISO 8601 string.
    /// </summary>
    /// <param name="writer">The writer of the enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <param name="value">The value; null leaves the member out.</param>
    public static void Write(Utf8JsonWriter writer, string name, DateTimeOffset? value)
    {
        if (value is { } time) writer.WriteString(name, time);
    }

    /// <summary>
    /// Writes a <see cref="TimeSpan"/> member as a constant-format string, <c>1.02:03:04</c>.
    /// </summary>
    /// <param name="writer">The writer of the enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <param name="value">The value; null leaves the member out.</param>
    public static void Write(Utf8JsonWriter writer, string name, TimeSpan? value)
    {
        if (value is { } span) writer.WriteString(name, span.ToString("c", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Writes a string list member as an array.
    /// </summary>
    /// <param name="writer">The writer of the enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <param name="values">The values; null leaves the member out.</param>
    public static void Write(Utf8JsonWriter writer, string name, IEnumerable<string>? values)
    {
        if (values is null) return;

        writer.WriteStartArray(name);

        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// Writes an enum member by its name, so reordering the enum keeps stored values.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="writer">The writer of the enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <param name="value">The value; null leaves the member out.</param>
    public static void WriteEnum<TEnum>(Utf8JsonWriter writer, string name, TEnum? value) where TEnum : struct, Enum
    {
        if (value is { } member) writer.WriteString(name, member.ToString());
    }

    /// <summary>
    /// Writes nested storable data as an object member.
    /// </summary>
    /// <typeparam name="TData">The type of the nested data.</typeparam>
    /// <param name="writer">The writer of the enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <param name="value">The data; null or data without any value leaves the member out.</param>
    public static void WriteStorable<TData>(Utf8JsonWriter writer, string name, TData? value)
        where TData : class, IStorable<TData>
    {
        if (value is not null) WriteRaw(writer, name, value.Serialize());
    }

    /// <summary>
    /// Writes an object member.
    /// </summary>
    /// <param name="writer">The writer of the enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <param name="writeMembers">
    /// Writes the members of the nested object; when it writes none, the member is left out.
    /// </param>
    public static void WriteObject(Utf8JsonWriter writer, string name, Action<Utf8JsonWriter> writeMembers) =>
        WriteRaw(writer, name, Write(writeMembers));

    /// <summary>
    /// Reads a string member.
    /// </summary>
    /// <param name="data">The enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <returns>The value, or null when the member is missing or not a string.</returns>
    public static string? ReadString(JsonElement data, string name) =>
        Find(data, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    /// <summary>
    /// Reads a Boolean member.
    /// </summary>
    /// <param name="data">The enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <returns>The value, or null when the member is missing or not a Boolean.</returns>
    public static bool? ReadBoolean(JsonElement data, string name) =>
        Find(data, name) switch
        {
            { ValueKind: JsonValueKind.True } => true,
            { ValueKind: JsonValueKind.False } => false,
            _ => null
        };

    /// <summary>
    /// Reads a number member.
    /// </summary>
    /// <typeparam name="TNumber">The number type.</typeparam>
    /// <param name="data">The enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <returns>The value, or null when the member is missing or not a number of that type.</returns>
    public static TNumber? ReadNumber<TNumber>(JsonElement data, string name)
        where TNumber : struct, INumber<TNumber> =>
        Find(data, name) is { ValueKind: JsonValueKind.Number } value
        && TNumber.TryParse(value.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;

    /// <summary>
    /// Reads a <see cref="Guid"/> member.
    /// </summary>
    /// <param name="data">The enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <returns>The value, or null when the member is missing or not a GUID string.</returns>
    public static Guid? ReadGuid(JsonElement data, string name) =>
        Text(data, name) is { } value && value.TryGetGuid(out var guid) ? guid : null;

    /// <summary>
    /// Reads a <see cref="DateTime"/> member.
    /// </summary>
    /// <param name="data">The enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <returns>The value, or null when the member is missing or not an ISO 8601 string.</returns>
    public static DateTime? ReadDateTime(JsonElement data, string name) =>
        Text(data, name) is { } value && value.TryGetDateTime(out var time) ? time : null;

    /// <summary>
    /// Reads a <see cref="DateTimeOffset"/> member.
    /// </summary>
    /// <param name="data">The enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <returns>The value, or null when the member is missing or not an ISO 8601 string.</returns>
    public static DateTimeOffset? ReadDateTimeOffset(JsonElement data, string name) =>
        Text(data, name) is { } value && value.TryGetDateTimeOffset(out var time) ? time : null;

    /// <summary>
    /// Reads a <see cref="TimeSpan"/> member.
    /// </summary>
    /// <param name="data">The enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <returns>The value, or null when the member is missing or not a time span string.</returns>
    public static TimeSpan? ReadTimeSpan(JsonElement data, string name) =>
        TimeSpan.TryParse(ReadString(data, name), CultureInfo.InvariantCulture, out var span) ? span : null;

    /// <summary>
    /// Reads a string list member.
    /// </summary>
    /// <param name="data">The enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <returns>The values, or null when the member is missing or not an array.</returns>
    public static string[]? ReadStrings(JsonElement data, string name) =>
        Find(data, name) is { ValueKind: JsonValueKind.Array } values
            ? [.. values.EnumerateArray().Where(IsString).Select(value => value.GetString()!)]
            : null;

    /// <summary>
    /// Reads an enum member by its name.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="data">The enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <returns>The value, or null when the member is missing or names no member of the enum.</returns>
    public static TEnum? ReadEnum<TEnum>(JsonElement data, string name) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(ReadString(data, name), out var member) ? member : null;

    /// <summary>
    /// Reads nested storable data.
    /// </summary>
    /// <typeparam name="TData">The type of the nested data.</typeparam>
    /// <param name="data">The enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <returns>The data, or null when the member is missing or not an object.</returns>
    public static TData? ReadStorable<TData>(JsonElement data, string name) where TData : class, IStorable<TData> =>
        Find(data, name) is { ValueKind: JsonValueKind.Object } value ? TData.Deserialize(value.GetRawText()) : null;

    /// <summary>
    /// Reads an object member, to read its own members from.
    /// </summary>
    /// <param name="data">The enclosing object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <returns>The object; a missing member reads as an element every read on returns null for.</returns>
    public static JsonElement ReadObject(JsonElement data, string name) =>
        Find(data, name) is { ValueKind: JsonValueKind.Object } value ? value : default;

    // An empty object says nothing a missing member does not, so it is left out like a null
    private static void WriteRaw(Utf8JsonWriter writer, string name, string json)
    {
        if (json is "{}") return;

        writer.WritePropertyName(name);
        writer.WriteRawValue(json);
    }

    private static JsonElement? Find(JsonElement data, string name) =>
        data.ValueKind is JsonValueKind.Object && data.TryGetProperty(name, out var value) ? value : null;

    private static JsonElement? Text(JsonElement data, string name) =>
        Find(data, name) is { ValueKind: JsonValueKind.String } value ? value : null;

    private static bool IsString(JsonElement value) => value.ValueKind is JsonValueKind.String;
}
