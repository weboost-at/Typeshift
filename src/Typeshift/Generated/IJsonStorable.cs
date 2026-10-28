using System.ComponentModel;
using System.Text.Json;

namespace Typeshift.Generated;

/// <summary>
/// Data that writes and reads its own JSON: an agent's <c>Settings</c>, a tool's overrides and a conversation entry.
/// </summary>
/// <typeparam name="TSelf">The data's type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IJsonStorable<TSelf> where TSelf : class, IJsonStorable<TSelf>
{
    /// <summary>
    /// Writes the data as a JSON object, leaving out null members.
    /// </summary>
    /// <param name="writer">The writer.</param>
    void WriteJson(Utf8JsonWriter writer);

    /// <summary>
    /// Reads data <see cref="WriteJson"/> wrote; missing or mistyped members read as null.
    /// </summary>
    /// <param name="element">The JSON.</param>
    /// <param name="value">The data.</param>
    /// <returns>False unless the JSON is an object.</returns>
    static abstract bool TryReadJson(JsonElement element, out TSelf value);
}
