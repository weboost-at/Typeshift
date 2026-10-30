using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Typeshift.Samples.Hosting;

// begin-snippet: tenant-chat-client-provider
internal sealed class TenantChatClientProvider(ICurrentCustomer customer, TenantClients clients) : IChatClientProvider
{
    public ValueTask<IChatClient> GetChatClientAsync(string provider, string model, CancellationToken ct) =>
        ValueTask.FromResult(clients.Get(customer.Id, provider));
}

// A singleton: a client keeps its connections open, so one per run would open new ones on every run
internal sealed class TenantClients(ITenantKeys keys) : IDisposable
{
    private readonly ConcurrentDictionary<(int Customer, string Provider), IChatClient> _clients = [];

    public IChatClient Get(int customer, string provider) =>
        _clients.GetOrAdd((customer, provider), key => new OpenAIClient(keys.GetApiKey(key.Customer, key.Provider))
            .GetChatClient("default")
            .AsIChatClient());

    // Call it when a customer changes a key, so the next run builds a client with the new one
    public void Forget(int customer, string provider)
    {
        if (_clients.TryRemove((customer, provider), out var client)) client.Dispose();
    }

    public void Dispose()
    {
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
    }
}
// end-snippet
