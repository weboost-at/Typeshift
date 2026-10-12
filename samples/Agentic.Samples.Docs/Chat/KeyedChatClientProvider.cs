using Agentic.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Samples.Docs.Chat;

// begin-snippet: chat-client-provider
internal sealed class KeyedChatClientProvider(IServiceProvider services) : IChatClientProvider
{
    public ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct) =>
        ValueTask.FromResult(services.GetRequiredKeyedService<IChatClient>(model));
}
// end-snippet
