using Agentic.Agents;
using Microsoft.Extensions.AI;

namespace Agentic.Tests.Testing.Fakes;

// Hands out the scripted client for whatever model an agent is configured with
public sealed class ScriptedChatClientProvider(ScriptedChatClient client) : IChatClientProvider
{
    public ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct) =>
        ValueTask.FromResult<IChatClient>(client);
}
