using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Returns the client of a provider's model. Resolved scoped from the run's services, so it may serve each customer
/// with their own credentials. A run asks the provider of its agent's provider type; without one, it takes the
/// <see cref="IChatClient"/> registered under its provider, or the one registered for every agent.
/// </summary>
/// <typeparam name="TProviderId">
/// The type of the providers it serves, as the agents name them in <c>WithModel(provider, model)</c>. When an agent's
/// <c>Settings</c> store the provider, a string, a number, a <see cref="Guid"/> or an enum (TSH029).
/// </typeparam>
public interface IChatClientProvider<TProviderId> where TProviderId : notnull
{
    /// <summary>
    /// Returns the client that serves a provider's model. It may be a new instance for each run, since Typeshift
    /// instruments each provider and model once, whatever instance it gets. Typeshift sends the model as
    /// <see cref="ChatOptions.ModelId"/> and never disposes the client.
    /// </summary>
    /// <param name="provider">The provider, as the agent's code or settings name it.</param>
    /// <param name="model">The model, as the provider's API names it.</param>
    /// <param name="ct">Cancels the lookup.</param>
    /// <returns>The client.</returns>
    ValueTask<IChatClient> GetChatClientAsync(TProviderId provider, string model, CancellationToken ct);
}
