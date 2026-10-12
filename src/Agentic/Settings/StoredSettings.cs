using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Agentic.Settings;

/// <summary>
/// Stored settings values as JSON by property name; a property without a stored value inherits it.
/// </summary>
public sealed record StoredSettings
{
    /// <summary>
    /// Settings that store no value.
    /// </summary>
    public static StoredSettings Empty { get; } = new();

    /// <summary>
    /// The stored values, keyed by property name.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> Values { get; init; } =
        ImmutableDictionary<string, JsonElement>.Empty;

    /// <summary>
    /// The stored value of the property, or null when it inherits.
    /// </summary>
    public T? GetValue<T>(string property, JsonSerializerOptions options) =>
        TryGetValue(property, out var value) ? value.Deserialize(GetTypeInfo<T>(options)) : default;

    /// <summary>
    /// Stores a value for the property, or removes it when the value is null so the property inherits again.
    /// </summary>
    public StoredSettings WithValue<T>(string property, T? value, JsonSerializerOptions options) =>
        With(property, value is null ? null : JsonSerializer.SerializeToElement(value, GetTypeInfo<T>(options)));

    /// <summary>
    /// Stores a JSON value for the property, or removes it when the value is null so the property inherits again.
    /// </summary>
    public StoredSettings With(string property, JsonElement? value) =>
        this with
        {
            Values = value is { ValueKind: not JsonValueKind.Null } stored
                ? Values.ToImmutableDictionary().SetItem(property, stored)
                : Values.ToImmutableDictionary().Remove(property)
        };

    // A JSON null that a store or another writer kept inherits, as a removed value does
    internal bool TryGetValue(string property, out JsonElement value) =>
        Values.TryGetValue(property, out value) && value.ValueKind is not JsonValueKind.Null;

    private static JsonTypeInfo<T> GetTypeInfo<T>(JsonSerializerOptions options) =>
        (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
}
