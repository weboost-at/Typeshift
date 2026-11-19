using System.Runtime.CompilerServices;
using Typeshift.Tests.Testing.Fakes;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Hosting.Support;

// The scripted client's updates, each arriving later on the pool, as a provider's arrive from the network: a run
// that awaits one where the caller's context is current comes back to that context
public sealed class NetworkChatClient(ScriptedChatClient inner) : IChatClient
{
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var update in inner.GetStreamingResponseAsync(messages, options, ct).ConfigureAwait(false))
        {
            await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
            yield return update;
        }
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken ct = default) =>
        GetStreamingResponseAsync(messages, options, ct).ToChatResponseAsync(ct);

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}

// Serves every model from the test's scripted client, over the network
public sealed class NetworkChatClientProvider(ScriptedChatClient client) : IChatClientProvider<string>
{
    public ValueTask<IChatClient> GetChatClientAsync(string provider, string model, CancellationToken ct) =>
        ValueTask.FromResult<IChatClient>(new NetworkChatClient(client));
}
