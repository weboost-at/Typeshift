using Microsoft.Extensions.AI;

namespace Agentic.Agents;

/// <summary>
/// Maps the model an agent is configured with to a chat client; the developer registers one.
/// </summary>
public interface IChatClientProvider
{
    /// <summary>
    /// A client for the model; throw <see cref="ConfigurationRequiredException"/> for a model it does not know.
    /// </summary>
    ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct);
}
