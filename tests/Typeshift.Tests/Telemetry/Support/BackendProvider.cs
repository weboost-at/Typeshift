using Typeshift.Tests.Testing.Fakes;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Telemetry.Support;

// Serves each call from a client of the next backend, as a provider that makes a client per customer or per model
public sealed class BackendProvider(ScriptedChatClient client, Backends backends) : IChatClientProvider<string>
{
    public ValueTask<IChatClient> GetChatClientAsync(string provider, string model, CancellationToken ct) =>
        ValueTask.FromResult<IChatClient>(new BackendClient(client, backends.Dequeue()));
}

// The backends of the clients the provider hands out, in order
public sealed class Backends(IEnumerable<ChatClientMetadata> backends) : Queue<ChatClientMetadata>(backends);
