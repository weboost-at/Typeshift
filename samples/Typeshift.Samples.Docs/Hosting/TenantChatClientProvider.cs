using Microsoft.Extensions.AI;
using OpenAI;

namespace Typeshift.Samples.Hosting;

// begin-snippet: tenant-chat-client-provider
internal sealed class TenantChatClientProvider(ICurrentCustomer customer, ITenantKeys keys) : IChatClientProvider
{
    // A client per run, with the customer's current key
    public async ValueTask<IChatClient> GetChatClientAsync(string provider, string model, CancellationToken ct)
    {
        if (provider != "openai") throw new InvalidOperationException($"No client for the provider '{provider}'.");

        var key = await keys.GetApiKeyAsync(customer.Id, provider, ct);

        return new OpenAIClient(key)
            .GetChatClient(model)
            .AsIChatClient();
    }
}
// end-snippet
