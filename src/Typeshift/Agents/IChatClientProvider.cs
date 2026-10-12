using Microsoft.Extensions.AI;

namespace Typeshift.Agents;

/// <summary>
/// Maps the model an agent is configured with to a chat client; the developer registers one.
/// </summary>
public interface IChatClientProvider
{
    /// <summary>
    /// Creates or hands out a client for a model.
    /// </summary>
    /// <param name="model">The model, as the agent's settings or chat options name it.</param>
    /// <param name="ct">Cancels the creation.</param>
    /// <returns>A client for the model; the library wraps it, so it may be shared.</returns>
    /// <exception cref="ConfigurationRequiredException">The provider does not know the model.</exception>
    ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct);
}
