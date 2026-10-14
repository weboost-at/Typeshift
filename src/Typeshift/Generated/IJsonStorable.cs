using System.ComponentModel;
using System.Text.Json;

namespace Typeshift.Generated;

/// <summary>
/// Generated settings that write and read their own JSON: an agent's <c>Settings</c> and a tool's overrides.
/// </summary>
/// <typeparam name="TSelf">The settings' type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IJsonStorable<TSelf> where TSelf : class, IJsonStorable<TSelf>
{
    /// <summary>
    /// Writes the settings as a JSON object, leaving out null members.
    /// </summary>
    /// <param name="writer">The writer.</param>
    void WriteJson(Utf8JsonWriter writer);

    /// <summary>
    /// Reads settings <see cref="WriteJson"/> wrote; missing or mistyped members read as null.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="value">The settings.</param>
    /// <returns>False unless the JSON is an object.</returns>
    static abstract bool TryReadJson(JsonElement element, out TSelf value);
}
