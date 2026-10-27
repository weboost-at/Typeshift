using Typeshift.Tests.Testing.Fakes;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Telemetry.Support;

// Serves every model from the test's scripted client in a new instance per call, as a provider of per-tenant clients
public sealed class ClientPerCallProvider(ScriptedChatClient client) : IChatClientProvider
{
    public ValueTask<IChatClient> GetChatClientAsync(string provider, string model, CancellationToken ct) =>
        ValueTask.FromResult<IChatClient>(new CallClient(client));

    private sealed class CallClient(IChatClient inner) : DelegatingChatClient(inner);
}
