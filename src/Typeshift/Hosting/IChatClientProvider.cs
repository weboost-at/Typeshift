using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Maps a model name to the client that serves it. Resolved from the run's services, so it may pick a customer's own
/// credentials.
/// </summary>
public interface IChatClientProvider
{
    /// <summary>
    /// Returns the client of a model, the same instance for the same model where possible, since Typeshift
    /// instruments each instance once and keeps that as long as the instance lives. Typeshift never disposes it.
    /// </summary>
    /// <param name="model">The model's name, as the agent's options or settings name it.</param>
    /// <param name="ct">Cancels the lookup.</param>
    /// <returns>The client.</returns>
    ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct);
}
