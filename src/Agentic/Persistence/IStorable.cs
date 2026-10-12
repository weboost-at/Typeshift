namespace Agentic.Persistence;

/// <summary>
/// Data the library hands to an <see cref="IAgenticStore{TId}"/>: it turns itself into JSON and back, so a store
/// needs no knowledge of its shape.
/// </summary>
/// <typeparam name="TSelf">The implementing type.</typeparam>
public interface IStorable<TSelf> where TSelf : class, IStorable<TSelf>
{
    /// <summary>
    /// Writes this data as a JSON document.
    /// </summary>
    /// <returns>The JSON that <see cref="Deserialize"/> reads back into equal data.</returns>
    string Serialize();

    /// <summary>
    /// Reads data that <see cref="Serialize"/> wrote.
    /// </summary>
    /// <param name="json">The JSON document.</param>
    /// <returns>The data; members the document lacks keep their defaults.</returns>
    /// <exception cref="System.Text.Json.JsonException">The text is not a JSON object.</exception>
    static abstract TSelf Deserialize(string json);
}
