using System.Buffers;
using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
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
/// Checks a value of JSON from outside a store, such as a request's body.
/// </summary>
/// <param name="element">The JSON.</param>
/// <param name="path">Where the value lies in the JSON, for the message.</param>
/// <exception cref="JsonException">The value does not read.</exception>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate void StorableCheck(JsonElement element, string path);

/// <summary>
/// Writes and reads the JSON of generated settings, value by value and without reflection. Generated code calls it; a
/// missing or mistyped value reads as absent, so it keeps the layer below. A stored provider that does not read is
/// refused instead, since the model beside it would go to the provider of the layer below.
/// </summary>
// D44: source generators do not see each other's output, so no JsonSerializerContext can cover generated settings
[EditorBrowsable(EditorBrowsableState.Never)]
public static class StorableJson
{
    private const string SchemaVersionMember = "$schemaVersion";

    // D44: text outside ASCII is stored as written; quotes and the characters HTML reads stay escaped, since an API may
    // pass stored JSON on as it is
    internal static readonly JavaScriptEncoder Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);

    /// <summary>
    /// Writes a document, with text outside ASCII as it is.
    /// </summary>
    /// <param name="write">Writes the document's value.</param>
    /// <returns>The JSON text.</returns>
    public static string Write(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = Encoder }))
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
    /// Writes a document's schema version as its <c>$schemaVersion</c> member, left out at 1.
    /// </summary>
    /// <param name="writer">The writer, inside the document's object.</param>
    /// <param name="version">The schema version.</param>
    // D40: the schema version travels in the JSON, so a store keeps the JSON and nothing next to it
    public static void WriteSchemaVersion(Utf8JsonWriter writer, int version)
    {
        if (version > 1) writer.WriteNumber(SchemaVersionMember, version);
    }

    /// <summary>
    /// Refuses a document written under a newer schema version than the code's; a missing one is 1.
    /// </summary>
    /// <param name="element">The document's object.</param>
    /// <param name="version">The code's schema version.</param>
    /// <param name="kind">The document's kind, for the message.</param>
    /// <exception cref="JsonException">The document's schema version is newer, or no whole number.</exception>
    public static void EnsureSchemaVersion(JsonElement element, int version, string kind)
    {
        var written = GetSchemaVersion(element, kind);
        if (written > version)
            throw new JsonException($"'{kind}' of schema version {written} is newer than this code's {version}.");
    }

    // A schema version that is no whole number cannot be shown to be older, so it is refused, not read as missing
    private static int GetSchemaVersion(JsonElement element, string kind) =>
        Member(element, SchemaVersionMember) switch
        {
            { ValueKind: JsonValueKind.Undefined } => 1,
            { ValueKind: JsonValueKind.Number } stored when stored.TryGetInt32(out var written) => written,
            _ => throw new JsonException($"The {SchemaVersionMember} of '{kind}' must be a whole number.")
        };

    /// <summary>
    /// Refuses a document whose stored provider is no value of the agent's provider id type, such as the name of an
    /// enum member the enum no longer has. A missing provider and a JSON null keep the layer below.
    /// </summary>
    /// <typeparam name="TProviderId">The agent's provider id type.</typeparam>
    /// <param name="element">The document's object.</param>
    /// <param name="name">The provider's JSON name.</param>
    /// <param name="read">Reads a provider of the type.</param>
    /// <param name="kind">The document's kind, for the message.</param>
    /// <exception cref="JsonException">The stored provider is no value of the type.</exception>
    // D86: read as absent, the provider would leave the model stored beside it to the provider of the layer below
    public static void EnsureProvider<TProviderId>(
        JsonElement element,
        string name,
        StorableReader<TProviderId> read,
        string kind)
    {
        var stored = Member(element, name);
        if (stored.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null || read(stored, out _)) return;

        throw new JsonException(
            $"The '{kind}' settings store the {name} {stored.GetRawText()}, which is no "
            + $"{TypeNames.Of<TProviderId>()}, the type the agent names its providers by: change the stored value to "
            + "a provider of that type.");
    }

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
    /// Writes an enum value by its name, so reordering the enum keeps stored values, and a value without a name, such
    /// as flags with none set or with a bit no member names, as its number.
    /// </summary>
    /// <typeparam name="TEnum">The enum's type.</typeparam>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value.</param>
    public static void WriteEnum<TEnum>(Utf8JsonWriter writer, TEnum value) where TEnum : struct, Enum
    {
        var text = value.ToString();

        if (IsNamed<TEnum>(text)) writer.WriteStringValue(text);
        else writer.WriteRawValue(GetNumber(value));
    }

    /// <summary>
    /// Writes a dictionary's enum key as a member name: the value's name, or the number of a value without one, as
    /// <see cref="WriteEnum{TEnum}"/> writes a value.
    /// </summary>
    /// <typeparam name="TEnum">The enum's type.</typeparam>
    /// <param name="writer">The writer, inside the dictionary's object.</param>
    /// <param name="key">The key.</param>
    public static void WriteEnumKey<TEnum>(Utf8JsonWriter writer, TEnum key) where TEnum : struct, Enum
    {
        var text = key.ToString();

        writer.WritePropertyName(IsNamed<TEnum>(text) ? text : GetNumber(key));
    }

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
    /// Reads an enum value by its name, a combination of flags, or the number of a value without a name, as
    /// <see cref="WriteEnum{TEnum}"/> writes them. The number reads from a string of digits too, which is how
    /// versions up to 0.1.0-alpha.6 wrote it.
    /// </summary>
    /// <typeparam name="TEnum">The enum's type.</typeparam>
    /// <param name="element">The JSON.</param>
    /// <param name="value">The value.</param>
    /// <returns>False unless the JSON names members of the enum or is a whole number of its range.</returns>
    public static bool TryReadEnum<TEnum>(JsonElement element, out TEnum value) where TEnum : struct, Enum
    {
        value = default;

        return element.ValueKind switch
        {
            JsonValueKind.String => TryParseEnum(element.GetString()!, out value),
            JsonValueKind.Number => TryParseEnum(element.GetRawText(), out value),
            _ => false
        };
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
    /// Reads a <see cref="TimeSpan"/> in the constant format, as it is written.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="value">The value.</param>
    /// <returns>False unless the JSON is a time span string in the constant format.</returns>
    // The constant format still parses "30" as thirty days and "1:30" as ninety minutes, which a hand edit hardly
    // means, so a text reads only when it is what the value is written as
    public static bool TryRead(JsonElement element, out TimeSpan value)
    {
        if (TryRead(element, out string text)
            && TimeSpan.TryParseExact(text, "c", CultureInfo.InvariantCulture, out value)
            && value.ToString("c", CultureInfo.InvariantCulture) == text)
            return true;

        value = default;
        return false;
    }

    /// <summary>
    /// Reads a <see cref="DateOnly"/> in ISO 8601, as it is written.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="value">The value.</param>
    /// <returns>False unless the JSON is an ISO 8601 date string, <c>2026-01-02</c>.</returns>
    public static bool TryRead(JsonElement element, out DateOnly value)
    {
        value = default;

        return TryRead(element, out string text)
            && DateOnly.TryParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    /// <summary>
    /// Reads a <see cref="TimeOnly"/> in ISO 8601, as it is written.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="value">The value.</param>
    /// <returns>False unless the JSON is an ISO 8601 time string, <c>13:45:30.0000000</c>.</returns>
    public static bool TryRead(JsonElement element, out TimeOnly value)
    {
        value = default;

        return TryRead(element, out string text)
            && TimeOnly.TryParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
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
    /// Reads an object as a dictionary keyed by enum names, or by the number of a value without a name, leaving out
    /// the entries that do not read.
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
            if (TryParseEnum(name, out TKey key)) values[key] = value;
        }

        return true;
    }

    /// <summary>
    /// The path of a member, for a message: its name after the path of its object.
    /// </summary>
    /// <param name="path">The object's path, empty at the top.</param>
    /// <param name="name">The member's JSON name.</param>
    /// <returns>The member's path.</returns>
    public static string Path(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";

    /// <summary>
    /// Refuses an object of JSON from outside a store that holds a member besides <paramref name="members"/>, which
    /// a store's read would pass over. A missing object and a JSON null pass.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="path">Where the object lies in the JSON, for the message.</param>
    /// <param name="members">The JSON names of the members it may hold.</param>
    /// <exception cref="JsonException">The JSON is no object, or holds another member.</exception>
    // D44: a request's body is refused where a stored document reads as missing, so a PUT cannot erase settings
    public static void CheckMembers(JsonElement element, string path, ReadOnlySpan<string> members)
    {
        if (IsMissing(element)) return;
        if (element.ValueKind is not JsonValueKind.Object) throw Unread(element, path, "JSON object");

        foreach (var member in element.EnumerateObject())
        {
            if (!members.Contains(member.Name)) throw Unknown(path, member.Name, members);
        }
    }

    /// <summary>
    /// Refuses a value of JSON from outside a store that does not read, which a store's read takes for missing. A
    /// missing value and a JSON null pass.
    /// </summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="element">The JSON.</param>
    /// <param name="path">Where the value lies in the JSON, for the message.</param>
    /// <param name="read">Reads a value of the type.</param>
    /// <exception cref="JsonException">The value does not read.</exception>
    public static void Check<T>(JsonElement element, string path, StorableReader<T> read)
    {
        if (!IsMissing(element) && !read(element, out _)) throw Unread(element, path, TypeNames.Of<T>());
    }

    /// <summary>
    /// Refuses an enum value of JSON from outside a store that names no member of the enum and is no whole number.
    /// A missing value and a JSON null pass.
    /// </summary>
    /// <typeparam name="TEnum">The enum's type.</typeparam>
    /// <param name="element">The JSON.</param>
    /// <param name="path">Where the value lies in the JSON, for the message.</param>
    /// <exception cref="JsonException">The value does not read.</exception>
    public static void CheckEnum<TEnum>(JsonElement element, string path) where TEnum : struct, Enum
    {
        if (IsMissing(element) || TryReadEnum(element, out TEnum _)) return;

        throw new JsonException(
            $"{GetUnread(element, path, TypeNames.Of<TEnum>())} Its names are {Quote(Enum.GetNames<TEnum>())}.");
    }

    /// <summary>
    /// Refuses reasoning options of JSON from outside a store with a member besides <c>effort</c> and
    /// <c>output</c>, or with one that names no value. Missing options and a JSON null pass.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="path">Where the options lie in the JSON, for the message.</param>
    /// <exception cref="JsonException">The options do not read.</exception>
    public static void CheckReasoning(JsonElement element, string path)
    {
        CheckMembers(element, path, ["effort", "output"]);
        CheckEnum<ReasoningEffort>(Member(element, "effort"), Path(path, "effort"));
        CheckEnum<ReasoningOutput>(Member(element, "output"), Path(path, "output"));
    }

    /// <summary>
    /// Refuses an array of JSON from outside a store with an item that does not read, which a store's read leaves
    /// out. A missing array and a JSON null pass.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="path">Where the array lies in the JSON, for the message.</param>
    /// <param name="check">Checks an item.</param>
    /// <exception cref="JsonException">The JSON is no array, or an item is null or does not read.</exception>
    public static void CheckItems(JsonElement element, string path, StorableCheck check)
    {
        if (IsMissing(element)) return;
        if (element.ValueKind is not JsonValueKind.Array) throw Unread(element, path, "JSON array");

        var index = 0;

        foreach (var item in element.EnumerateArray())
        {
            CheckValue(item, $"{path}[{index++}]", check);
        }
    }

    /// <summary>
    /// Refuses an object of JSON from outside a store, read as a dictionary, with a value that does not read, which a
    /// store's read leaves out. A missing object and a JSON null pass.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="path">Where the object lies in the JSON, for the message.</param>
    /// <param name="check">Checks a value.</param>
    /// <exception cref="JsonException">The JSON is no object, or a value is null or does not read.</exception>
    public static void CheckValues(JsonElement element, string path, StorableCheck check)
    {
        if (IsMissing(element)) return;
        if (element.ValueKind is not JsonValueKind.Object) throw Unread(element, path, "JSON object");

        foreach (var member in element.EnumerateObject())
        {
            CheckValue(member.Value, Path(path, member.Name), check);
        }
    }

    /// <summary>
    /// Refuses an object of JSON from outside a store, read as a dictionary keyed by an enum, with a key that names no
    /// value or a value that does not read, both of which a store's read leaves out. A missing object and a JSON null
    /// pass.
    /// </summary>
    /// <typeparam name="TKey">The keys' enum type.</typeparam>
    /// <param name="element">The JSON.</param>
    /// <param name="path">Where the object lies in the JSON, for the message.</param>
    /// <param name="check">Checks a value.</param>
    /// <exception cref="JsonException">
    /// The JSON is no object, a key names no value of the enum, or a value is null or does not read.
    /// </exception>
    public static void CheckEnumKeys<TKey>(JsonElement element, string path, StorableCheck check)
        where TKey : struct, Enum
    {
        CheckValues(element, path, check);
        if (IsMissing(element)) return;

        foreach (var member in element.EnumerateObject())
        {
            if (TryParseEnum(member.Name, out TKey _)) continue;

            throw new JsonException(
                $"The key '{member.Name}' of '{path}' is no {TypeNames.Of<TKey>()}. Its names are "
                + $"{Quote(Enum.GetNames<TKey>())}.");
        }
    }

    private static void CheckValue(JsonElement element, string path, StorableCheck check)
    {
        if (element.ValueKind is JsonValueKind.Null)
            throw new JsonException($"'{path}' is null, which an item of settings cannot be.");

        check(element, path);
    }

    private static bool IsMissing(JsonElement element) =>
        element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null;

    private static JsonException Unread(JsonElement element, string path, string type) =>
        new(GetUnread(element, path, type));

    private static string GetUnread(JsonElement element, string path, string type)
    {
        var value = element.ValueKind switch
        {
            JsonValueKind.Object => "an object",
            JsonValueKind.Array => "an array",
            _ => element.GetRawText()
        };

        return $"'{path}' holds {value}, which is no {type}.";
    }

    // Names are case-sensitive, while ASP.NET binds its other bodies without regard to case, so a name that differs
    // only in case is the likely mistake
    private static JsonException Unknown(string path, string name, ReadOnlySpan<string> members)
    {
        List<string> names = [];

        foreach (var member in members)
        {
            if (!member.StartsWith('$')) names.Add(member);
        }

        var unknown = $"'{Path(path, name)}' is no settings member";
        var cased = names.Find(member => string.Equals(member, name, StringComparison.OrdinalIgnoreCase));
        var owner = path.Length == 0 ? "the settings" : $"'{path}'";

        return new JsonException(
            cased is not null ? $"{unknown}: member names are camelCase, such as '{Path(path, cased)}'."
            : names.Count == 0 ? $"{unknown}: these settings have no members."
            : $"{unknown}. The members of {owner} are {Quote(names)}.");
    }

    // 'a', 'b' and 'c'
    private static string Quote(IReadOnlyList<string> names) =>
        names.Count == 1
            ? $"'{names[0]}'"
            : $"{string.Join(", ", names.Take(names.Count - 1).Select(name => $"'{name}'"))} and '{names[^1]}'";

    // Names, each a member's, or a whole number, so text that is neither reads as absent
    private static bool TryParseEnum<TEnum>(string text, out TEnum value) where TEnum : struct, Enum
    {
        value = default;
        return (IsNamed<TEnum>(text) || IsWholeNumber(text)) && Enum.TryParse(text, out value);
    }

    private static bool IsNamed<TEnum>(string text) where TEnum : struct, Enum =>
        text.Split(", ").All(part => Enum.IsDefined(typeof(TEnum), part));

    private static bool IsWholeNumber(string text)
    {
        var digits = text.AsSpan(text.StartsWith('-') ? 1 : 0);
        return digits.Length > 0 && !digits.ContainsAnyExceptInRange('0', '9');
    }

    // ToString puts the current culture's sign before a negative value, which no reader parses back
    private static string GetNumber<TEnum>(TEnum value) where TEnum : struct, Enum =>
        value.GetTypeCode() is TypeCode.UInt64
            ? Convert.ToUInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
            : Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

    private static bool Parse<TNumber>(string text, out TNumber value) where TNumber : struct, INumber<TNumber> =>
        TNumber.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
