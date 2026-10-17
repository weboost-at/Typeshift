using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Returns the client of a provider's model. Resolved scoped from the run's services, so it may serve each customer
/// with their own credentials. Implement <c>IChatClientProvider</c>, the alias the generator writes for the build's
/// provider id type.
/// </summary>
/// <typeparam name="TProviderId">The build's provider id type, <c>TypeshiftProviderIdType</c>.</typeparam>
public interface IChatClientProvider<TProviderId> where TProviderId : notnull
{
    /// <summary>
    /// Returns the client that serves a provider's model, the same instance for the same pair where possible, since
    /// Typeshift instruments each instance once and keeps that as long as the instance lives. Typeshift sends the
    /// model as <see cref="ChatOptions.ModelId"/> and never disposes the client.
    /// </summary>
    /// <param name="provider">The provider, as the agent's code or settings name it.</param>
    /// <param name="model">The model, as the provider's API names it.</param>
    /// <param name="ct">Cancels the lookup.</param>
    /// <returns>The client.</returns>
    ValueTask<IChatClient> GetChatClientAsync(TProviderId provider, string model, CancellationToken ct);
}
