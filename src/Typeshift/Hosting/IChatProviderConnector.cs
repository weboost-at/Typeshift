using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Turns a <see cref="ChatProvider"/> your app hands a run into the chat client that serves it. Register yours with
/// <c>AddTypeshift().WithProviderConnector&lt;TConnector&gt;()</c>; Typeshift resolves it from the services of the run
/// that meets a document it holds no client for, and calls it once for runs that hand in equal documents at the same
/// time. The client then serves every run handed an equal document, so build it from the document alone. Typeshift
/// owns the client: it disposes it once no run used it for <c>TypeshiftOptions.ProviderClientIdleTime</c>, and when the
/// runtime is disposed.
/// More in the guide:
/// <see href="https://weboost-at.github.io/Typeshift/guide/models#providers-your-customers-bring">Providers your
/// customers bring</see>.
/// </summary>
public interface IChatProviderConnector
{
    /// <summary>
    /// Builds the client of a provider. An exception fails the run that handed the document in, before it stores
    /// anything.
    /// </summary>
    /// <param name="provider">
    /// The document as your app handed it to the run, of your record's type when your app derives one.
    /// </param>
    /// <param name="ct">Cancels the connection.</param>
    /// <returns>The client, which Typeshift owns from now on.</returns>
    ValueTask<IChatClient> ConnectAsync(ChatProvider provider, CancellationToken ct);
}
