using System.Buffers;
using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Typeshift.Generated;

/// <summary>
/// Reads a stored value from JSON.
/// </summary>
/// <typeparam name="T">The value's type.</typeparam>
/// <param name="element">The JSON.</param>
/// <param name="value">The value, when the JSON holds one of the type.</param>
/// <returns>False when the JSON is missing or holds something else.</returns>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate bool StorableReader<T>(JsonElement element, out T value);

/// <summary>
/// Writes and reads the JSON of generated settings, value by value and without reflection. Generated code calls it; a
/// missing or mistyped value reads as absent, so it keeps the layer below.
/// </summary>
// D44: source generators do not see each other's output, so no JsonSerializerContext can cover generated settings
[EditorBrowsable(EditorBrowsableState.Never)]
public static class StorableJson
{
    private const string VersionMember = "$version";

    /// <summary>
    /// Writes a document.
    /// </summary>
    /// <param name="write">Writes the document's value.</param>
    /// <returns>The JSON text.</returns>
    public static string Write(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// Reads a document that is a JSON object.
    /// </summary>
    /// <typeparam name="T">The document's type.</typeparam>
    /// <param name="json">The JSON text.</param>
    /// <param name="read">Reads the document from the object.</param>
    /// <returns>The document.</returns>
    /// <exception cref="JsonException">The text is no JSON object.</exception>
    public static T Read<T>(string json, StorableReader<T> read)
    {
        using var document = JsonDocument.Parse(json);

        return read(document.RootElement, out var value)
            ? value
            : throw new JsonException($"Stored data must be a JSON object, not {document.RootElement.ValueKind}.");
    }

    /// <summary>
    /// Writes a document's schema version as its <c>$version</c> member, left out at version 1.
    /// </summary>
    /// <param name="writer">The writer, inside the document's object.</param>
    /// <param name="version">The version.</param>
    // D40: the version travels in the JSON, so a store keeps the JSON and nothing next to it
    public static void WriteVersion(Utf8JsonWriter writer, int version)
    {
        if (version > 1) writer.WriteNumber(VersionMember, version);
    }

    /// <summary>
    /// Refuses a document written under a newer schema version than the code's; a missing version is 1.
    /// </summary>
    /// <param name="element">The document's object.</param>
    /// <param name="version">The code's version.</param>
    /// <param name="kind">The document's kind, for the message.</param>
    /// <exception cref="JsonException">The document's version is newer, or no whole number.</exception>
    public static void EnsureVersion(JsonElement element, int version, string kind)
    {
        var written = GetVersion(element, kind);
        if (written > version)
            throw new JsonException($"'{kind}' of version {written} is newer than this code's version {version}.");
    }

    // A version that is no whole number cannot be shown to be older, so it is refused rather than read as missing
    private static int GetVersion(JsonElement element, string kind) =>
        Member(element, VersionMember) switch
        {
            { ValueKind: JsonValueKind.Undefined } => 1,
            { ValueKind: JsonValueKind.Number } stored when stored.TryGetInt32(out var written) => written,
            _ => throw new JsonException($"The {VersionMember} of '{kind}' must be a whole number.")
        };

    /// <summary>
    /// Finds a member of an object.
    /// </summary>
    /// <param name="element">The object.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <returns>The member's value; for a missing one, a value every read refuses.</returns>
    public static JsonElement Member(JsonElement element, string name) =>
        element.ValueKind is JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

    /// <summary>
    /// Writes a string.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    public static void Write(Utf8JsonWriter writer, string value) => writer.WriteStringValue(value);

    /// <summary>
    /// Writes a Boolean.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    public static void Write(Utf8JsonWriter writer, bool value) => writer.WriteBooleanValue(value);

    /// <summary>
    /// Writes a number; NaN and the infinities, which JSON numbers cannot hold, as strings.
    /// </summary>
    /// <typeparam name="TNumber">The number's type.</typeparam>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    public static void WriteNumber<TNumber>(Utf8JsonWriter writer, TNumber value)
        where TNumber : struct, INumber<TNumber>
    {
        var text = value.ToString(null, CultureInfo.InvariantCulture);

        if (TNumber.IsFinite(value)) writer.WriteRawValue(text);
        else writer.WriteStringValue(text);
    }

    /// <summary>
    /// Writes an enum value by its name, so reordering the enum keeps stored values.
    /// </summary>
    /// <typeparam name="TEnum">The enum's type.</typeparam>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    public static void WriteEnum<TEnum>(Utf8JsonWriter writer, TEnum value) where TEnum : struct, Enum =>
        writer.WriteStringValue(value.ToString());

    /// <summary>
    /// Writes a <see cref="Guid"/>.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    public static void Write(Utf8JsonWriter writer, Guid value) => writer.WriteStringValue(value);

    /// <summary>
    /// Writes a <see cref="DateTime"/> in ISO 8601.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    public static void Write(Utf8JsonWriter writer, DateTime value) => writer.WriteStringValue(value);

    /// <summary>
    /// Writes a <see cref="DateTimeOffset"/> in ISO 8601.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    public static void Write(Utf8JsonWriter writer, DateTimeOffset value) => writer.WriteStringValue(value);

    /// <summary>
    /// Writes a <see cref="TimeSpan"/> in the constant format, <c>1.02:03:04</c>.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    public static void Write(Utf8JsonWriter writer, TimeSpan value) =>
        writer.WriteStringValue(value.ToString("c", CultureInfo.InvariantCulture));

    /// <summary>
    /// Writes a <see cref="DateOnly"/> in ISO 8601.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    public static void Write(Utf8JsonWriter writer, DateOnly value) =>
        writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));

    /// <summary>
    /// Writes a <see cref="TimeOnly"/> in ISO 8601.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    public static void Write(Utf8JsonWriter writer, TimeOnly value) =>
        writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));

    /// <summary>
    /// Writes a <see cref="Uri"/> as its original string.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    public static void Write(Utf8JsonWriter writer, Uri value) => writer.WriteStringValue(value.OriginalString);

    /// <summary>
    /// Writes reasoning options as an object of their enum names.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    public static void Write(Utf8JsonWriter writer, ReasoningOptions value)
    {
        writer.WriteStartObject();
        if (value.Effort is { } effort) writer.WriteString("effort", effort.ToString());
        if (value.Output is { } output) writer.WriteString("output", output.ToString());
        writer.WriteEndObject();
    }

    /// <summary>
    /// Reads a string.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="value">The value.</param>
    /// <returns>False unless the JSON is a string.</returns>
    public static bool TryRead(JsonElement element, out string value)
    {
        value = element.ValueKind is JsonValueKind.String ? element.GetString()! : null!;
        return value is not null;
    }

    /// <summary>
    /// Reads a Boolean.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="value">The value.</param>
    /// <returns>False unless the JSON is true or false.</returns>
    public static bool TryRead(JsonElement element, out bool value)
    {
        value = element.ValueKind is JsonValueKind.True;
        return element.ValueKind is JsonValueKind.True or JsonValueKind.False;
    }

    /// <summary>
    /// Reads a number, or NaN or an infinity as <see cref="WriteNumber{TNumber}"/> writes them.
    /// </summary>
    /// <typeparam name="TNumber">The number's type.</typeparam>
    /// <param name="element">The JSON.</param>
    /// <param name="value">The value.</param>
    /// <returns>False unless the JSON is a number of the type.</returns>
    public static bool TryReadNumber<TNumber>(JsonElement element, out TNumber value)
        where TNumber : struct, INumber<TNumber>
    {
        value = default;

        return element.ValueKind switch
        {
            JsonValueKind.Number => Parse(element.GetRawText(), out value),
            JsonValueKind.String => Parse(element.GetString()!, out value) && !TNumber.IsFinite(value),
            _ => false
        };
    }

    /// <summary>
    /// Reads an enum value by its name, or a combination of flags as <see cref="WriteEnum{TEnum}"/> writes it.
    /// </summary>
    /// <typeparam name="TEnum">The enum's type.</typeparam>
    /// <param name="element">The JSON.</param>
    /// <param name="value">The value.</param>
    /// <returns>False unless the JSON names members of the enum.</returns>
    public static bool TryReadEnum<TEnum>(JsonElement element, out TEnum value) where TEnum : struct, Enum
    {
        value = default;

        return TryRead(element, out string text)
            && text.Split(", ").All(part => Enum.IsDefined(typeof(TEnum), part))
            && Enum.TryParse(text, out value);
    }

    /// <summary>
    /// Reads a <see cref="Guid"/>.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="value">The value.</param>
    /// <returns>False unless the JSON is a GUID string.</returns>
    public static bool TryRead(JsonElement element, out Guid value)
    {
        value = default;
        return element.ValueKind is JsonValueKind.String && element.TryGetGuid(out value);
    }

    /// <summary>
    /// Reads a <see cref="DateTime"/>.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="value">The value.</param>
    /// <returns>False unless the JSON is an ISO 8601 string.</returns>
    public static bool TryRead(JsonElement element, out DateTime value)
    {
        value = default;
        return element.ValueKind is JsonValueKind.String && element.TryGetDateTime(out value);
    }

    /// <summary>
    /// Reads a <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="value">The value.</param>
    /// <returns>False unless the JSON is an ISO 8601 string.</returns>
    public static bool TryRead(JsonElement element, out DateTimeOffset value)
    {
        value = default;
        return element.ValueKind is JsonValueKind.String && element.TryGetDateTimeOffset(out value);
    }

    /// <summary>
    /// Reads a <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="value">The value.</param>
    /// <returns>False unless the JSON is a time span string.</returns>
    public static bool TryRead(JsonElement element, out TimeSpan value)
    {
        value = default;
        return TryRead(element, out string text) && TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Reads a <see cref="DateOnly"/>.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="value">The value.</param>
    /// <returns>False unless the JSON is an ISO 8601 date string.</returns>
    public static bool TryRead(JsonElement element, out DateOnly value)
    {
        value = default;
        return TryRead(element, out string text) && DateOnly.TryParse(text, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Reads a <see cref="TimeOnly"/>.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="value">The value.</param>
    /// <returns>False unless the JSON is an ISO 8601 time string.</returns>
    public static bool TryRead(JsonElement element, out TimeOnly value)
    {
        value = default;
        return TryRead(element, out string text) && TimeOnly.TryParse(text, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Reads a <see cref="Uri"/>, absolute or relative.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="value">The value.</param>
    /// <returns>False unless the JSON is a URI string.</returns>
    public static bool TryRead(JsonElement element, out Uri value)
    {
        value = null!;
        return TryRead(element, out string text) && Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out value!);
    }

    /// <summary>
    /// Reads reasoning options.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="value">The value.</param>
    /// <returns>False unless the JSON is an object.</returns>
    public static bool TryRead(JsonElement element, out ReasoningOptions value)
    {
        value = null!;
        if (element.ValueKind is not JsonValueKind.Object) return false;

        value = new ReasoningOptions
        {
            Effort = TryReadEnum(Member(element, "effort"), out ReasoningEffort effort) ? effort : null,
            Output = TryReadEnum(Member(element, "output"), out ReasoningOutput output) ? output : null
        };

        return true;
    }

    /// <summary>
    /// Reads an array, leaving out the items that do not read.
    /// </summary>
    /// <typeparam name="T">The items' type.</typeparam>
    /// <param name="element">The JSON.</param>
    /// <param name="read">Reads an item.</param>
    /// <param name="values">The items.</param>
    /// <returns>False unless the JSON is an array.</returns>
    public static bool TryReadList<T>(JsonElement element, StorableReader<T> read, out List<T> values)
    {
        values = null!;
        if (element.ValueKind is not JsonValueKind.Array) return false;

        values = [];

        foreach (var item in element.EnumerateArray())
        {
            if (read(item, out var value)) values.Add(value);
        }

        return true;
    }

    /// <summary>
    /// Reads an object as a dictionary keyed by its member names, leaving out the values that do not read.
    /// </summary>
    /// <typeparam name="T">The values' type.</typeparam>
    /// <param name="element">The JSON.</param>
    /// <param name="read">Reads a value.</param>
    /// <param name="values">The entries.</param>
    /// <returns>False unless the JSON is an object.</returns>
    public static bool TryReadMap<T>(JsonElement element, StorableReader<T> read, out Dictionary<string, T> values)
    {
        values = null!;
        if (element.ValueKind is not JsonValueKind.Object) return false;

        values = [];

        foreach (var member in element.EnumerateObject())
        {
            if (read(member.Value, out var value)) values[member.Name] = value;
        }

        return true;
    }

    /// <summary>
    /// Reads an object as a dictionary keyed by enum names, leaving out the entries that do not read.
    /// </summary>
    /// <typeparam name="TKey">The keys' enum type.</typeparam>
    /// <typeparam name="T">The values' type.</typeparam>
    /// <param name="element">The JSON.</param>
    /// <param name="read">Reads a value.</param>
    /// <param name="values">The entries.</param>
    /// <returns>False unless the JSON is an object.</returns>
    public static bool TryReadEnumMap<TKey, T>(
        JsonElement element,
        StorableReader<T> read,
        out Dictionary<TKey, T> values)
        where TKey : struct, Enum
    {
        values = null!;
        if (!TryReadMap(element, read, out var named)) return false;

        values = [];

        foreach (var (name, value) in named)
        {
            if (Enum.IsDefined(typeof(TKey), name) && Enum.TryParse(name, out TKey key)) values[key] = value;
        }

        return true;
    }

    private static bool Parse<TNumber>(string text, out TNumber value) where TNumber : struct, INumber<TNumber> =>
        TNumber.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
