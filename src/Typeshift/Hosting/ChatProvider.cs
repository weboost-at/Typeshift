using System.Text;

namespace Typeshift;

// D88: a provider is a document your app owns: Typeshift stores none, and keeps one client per equal document
/// <summary>
/// A provider your app owns, such as a customer's own account: what reaches one vendor account or one endpoint.
/// Derive from it to add what your app needs, such as an id or an owner, keep it in your app's storage, and hand it
/// to a run with <c>host.WithProvider(provider)</c>. Your <see cref="IChatProviderConnector"/> turns it into the chat
/// client that serves the run, and Typeshift keeps that client for every run handed an equal document, so a rotated
/// key or a new endpoint is a new client. <see cref="ToString"/> leaves the <see cref="ApiKey"/> out, and so do
/// Typeshift's spans, logs and messages.
/// More in the guide:
/// <see href="https://weboost-at.github.io/Typeshift/guide/models#providers-your-customers-bring">Providers your
/// customers bring</see>.
/// </summary>
public record ChatProvider
{
    /// <summary>
    /// Which kind of client your connector builds for it, such as <c>"openai"</c>.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// A name for people, which spans, entries and messages show for the provider; null for its <see cref="Kind"/>.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The address the client calls; null for the vendor's own.
    /// </summary>
    public Uri? Endpoint { get; init; }

    /// <summary>
    /// The key the client calls with, or a reference your connector resolves, such as a path in a vault. Typeshift
    /// writes it nowhere.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// The model a run asks for when nothing else names one; null to leave it to the client.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Writes the members for <see cref="ToString"/>, every one but <see cref="ApiKey"/>, as a record of yours that
    /// derives from it writes its own after them.
    /// </summary>
    /// <param name="builder">The text <see cref="ToString"/> builds.</param>
    /// <returns>True, since it writes members.</returns>
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Kind = {Kind}, Name = {Name}, Endpoint = {Endpoint}, Model = {Model}");
        return true;
    }
}
