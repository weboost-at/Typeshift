using System.ClientModel;
using System.Collections.Concurrent;
using Anthropic;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Typeshift.Demos.Providers;

/// <summary>
/// The canned models of a demo by name, for runs without a network.
/// </summary>
public sealed class CannedModels : Dictionary<string, IChatClient>;

/// <summary>
/// The clients built so far, by model. Typeshift never disposes a provider's client, so the app owns their lifetime.
/// </summary>
// CLUNKY 4: IChatClientProvider is scoped, one per request, but clients must live for the process, so the cache is a
// singleton of its own and the provider is a thin shell over it. Every host writes this pair.
public sealed class ClientCache : IDisposable
{
    private readonly ConcurrentDictionary<ModelRef, IChatClient> _clients = new();

    public IChatClient GetOrAdd(ModelRef model, Func<ModelRef, IChatClient> create) => _clients.GetOrAdd(model, create);

    public void Dispose()
    {
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
    }
}

/// <summary>
/// Maps a provider the user defined and one of its models to a client.
/// </summary>
// This library runs no generator, so it names the provider id type itself instead of the IChatClientProvider alias
public sealed class RoutingChatClientProvider(ProviderCatalog catalog, ClientCache cache, CannedModels canned)
    : IChatClientProvider<string>
{
    public ValueTask<IChatClient> GetChatClientAsync(string provider, string model, CancellationToken ct) =>
        ValueTask.FromResult(cache.GetOrAdd(new ModelRef(provider, model), Create));

    private IChatClient Create(ModelRef model)
    {
        var provider = catalog[model.Provider];
        var key = ProviderCatalog.Expand(provider.ApiKey);

        // CLUNKY 5: a mistyped model fails here, in the middle of a run, long after the settings UI saved it; nothing
        // validates a model name when ConfigureAsync stores it
        return provider.Kind switch
        {
            "openai" => OpenAi(provider, key).GetChatClient(model.Model).AsIChatClient(),
            "anthropic" => new AnthropicClient { ApiKey = key }.AsIChatClient(model.Model),
            "canned" => canned.GetValueOrDefault(model.Model)
                ?? throw new InvalidOperationException($"No canned model '{model.Model}' exists in this demo."),
            _ => throw new InvalidOperationException($"The provider kind '{provider.Kind}' is unknown.")
        };
    }

    private static OpenAIClient OpenAi(ProviderDefinition provider, string key) =>
        provider.Endpoint is { } endpoint
            ? new OpenAIClient(new ApiKeyCredential(key.Length > 0 ? key : "none"), new OpenAIClientOptions
            {
                Endpoint = new Uri(endpoint)
            })
            : new OpenAIClient(new ApiKeyCredential(key));
}
