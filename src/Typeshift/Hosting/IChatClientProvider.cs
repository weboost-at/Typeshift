using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Returns the client of a provider's model. Resolved scoped from the run's services, so it may serve each customer
/// with their own credentials. A run that names a provider asks the provider class of its agent's provider type,
/// which then decides alone; without one, it takes the <see cref="IChatClient"/> registered under its provider. A
/// run that names no provider takes the default client and asks no provider class.
/// More in the guide:
/// <see href="https://weboost-at.github.io/Typeshift/guide/models#your-own-provider">Your own provider</see>.
/// </summary>
/// <typeparam name="TProviderId">
/// The type of the providers it serves, as the agents name them in <c>WithModel(provider, model)</c>. When an agent's
/// <c>Settings</c> store the provider, a string, a number, a <see cref="Guid"/> or an enum (TSH029).
/// </typeparam>
public interface IChatClientProvider<TProviderId> where TProviderId : notnull
{
    /// <summary>
    /// Returns the client that serves a provider's model. It may be a new instance for each run, since Typeshift
    /// instruments each backend once, by what the client's metadata says of it, whatever instance it gets. Typeshift
    /// sends the model as <see cref="ChatOptions.ModelId"/> and never disposes the client.
    /// </summary>
    /// <param name="provider">The provider, as the agent's code or settings name it.</param>
    /// <param name="model">The model, as the provider's API names it.</param>
    /// <param name="ct">Cancels the lookup.</param>
    /// <returns>The client.</returns>
    ValueTask<IChatClient> GetChatClientAsync(TProviderId provider, string model, CancellationToken ct);
}
