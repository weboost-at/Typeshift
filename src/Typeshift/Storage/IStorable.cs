namespace Typeshift;

/// <summary>
/// A document Typeshift stores: the agents' <c>Settings</c>, a closed set the generator writes.
/// </summary>
/// <typeparam name="TSelf">The document's type.</typeparam>
public interface IStorable<TSelf> where TSelf : class, IStorable<TSelf>
{
    /// <summary>
    /// The stable name a store keys the kind by: for an agent's settings its <c>WithStorageKind</c>, or else its class
    /// name, so moving it to another namespace keeps its data.
    /// </summary>
    static abstract string Kind { get; }

    /// <summary>
    /// The schema version <see cref="Serialize"/> writes into the JSON as <c>$version</c>, left out at 1, and
    /// <see cref="Deserialize"/> checks.
    /// </summary>
    static abstract int Version { get; }

    /// <summary>
    /// Writes the document as JSON.
    /// </summary>
    /// <returns>The JSON text.</returns>
    string Serialize();

    /// <summary>
    /// Reads a document <see cref="Serialize"/> wrote, under the version its JSON names, 1 when it names none.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The document.</returns>
    /// <exception cref="System.Text.Json.JsonException">
    /// The version is newer than this code's, or the text is no JSON object.
    /// </exception>
    static abstract TSelf Deserialize(string json);
}
