using System.Collections.Immutable;
using System.Text.Json;

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
    /// Stores a value for the property, or removes it when the value is null so the property inherits again.
    /// </summary>
    public StoredSettings With(string property, JsonElement? value) =>
        this with
        {
            Values = value is { } stored
                ? Values.ToImmutableDictionary().SetItem(property, stored)
                : Values.ToImmutableDictionary().Remove(property)
        };
}
