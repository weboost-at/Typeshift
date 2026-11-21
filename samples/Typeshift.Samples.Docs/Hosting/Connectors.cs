using System.ClientModel;
using Anthropic;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Typeshift.Samples.Hosting;

// begin-snippet: provider-connector
internal sealed class Connectors : IChatProviderConnector
{
    public ValueTask<IChatClient> ConnectAsync(ChatProvider provider, CancellationToken cancellationToken) =>
        ValueTask.FromResult(provider.Kind switch
        {
            "openai" => OpenAi(provider),
            "anthropic" => new AnthropicClient { ApiKey = provider.ApiKey }.AsIChatClient(provider.Model),
            _ => throw new InvalidOperationException($"No connector for the provider kind '{provider.Kind}'.")
        });

    // The document's key and endpoint, and its model as the client's own
    private static IChatClient OpenAi(ChatProvider provider)
    {
        var options = new OpenAIClientOptions();
        if (provider.Endpoint is { } endpoint) options.Endpoint = endpoint;

        var key = provider.ApiKey
            ?? throw new InvalidOperationException($"The provider '{provider.Name}' has no key.");

        return new OpenAIClient(new ApiKeyCredential(key), options)
            .GetChatClient(provider.Model ?? "gpt-5-mini")
            .AsIChatClient();
    }
}
// end-snippet

// The scenarios' connector, which serves every document from the canned client the guide's runs answer on
internal sealed class CannedConnector(IChatClient canned) : IChatProviderConnector
{
    public ValueTask<IChatClient> ConnectAsync(ChatProvider provider, CancellationToken cancellationToken) =>
        ValueTask.FromResult(canned);
}
