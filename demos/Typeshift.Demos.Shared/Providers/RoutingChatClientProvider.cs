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
/// The clients built so far, by model. Typeshift never disposes a provider's client and instruments each instance
/// once, so the same instance must come back for the same model, and the app owns their lifetime.
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
/// Maps the app's model names to clients of the providers the user defined.
/// </summary>
public sealed class RoutingChatClientProvider(ProviderCatalog catalog, ClientCache cache, CannedModels canned)
    : IChatClientProvider
{
    public ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct) =>
        ValueTask.FromResult(cache.GetOrAdd(ModelRef.Parse(catalog.Resolve(model)), Create));

    private IChatClient Create(ModelRef model)
    {
        var provider = catalog[model.Provider];
        var key = ProviderCatalog.Expand(provider.ApiKey);

        // CLUNKY 5: a mistyped model fails here, in the middle of a run, long after the settings UI saved it; nothing
        // validates a model name when ConfigureAsync stores it
        IChatClient client = provider.Kind switch
        {
            "openai" => OpenAi(provider, key).GetChatClient(model.Model).AsIChatClient(),
            "anthropic" => new AnthropicClient { ApiKey = key }.AsIChatClient(model.Model),
            "canned" => canned.GetValueOrDefault(model.Model)
                ?? throw new InvalidOperationException($"No canned model '{model.Model}' exists in this demo."),
            _ => throw new InvalidOperationException($"The provider kind '{provider.Kind}' is unknown.")
        };

        return new WireModelChatClient(client, model.Model);
    }

    private static OpenAIClient OpenAi(ProviderDefinition provider, string key) =>
        provider.Endpoint is { } endpoint
            ? new OpenAIClient(new ApiKeyCredential(key.Length > 0 ? key : "none"), new OpenAIClientOptions
            {
                Endpoint = new Uri(endpoint)
            })
            : new OpenAIClient(new ApiKeyCredential(key));
}

/// <summary>
/// Puts the provider's model id back into each request.
/// </summary>
// CLUNKY 6: the provider's client sends ChatOptions.ModelId as the request's model, and Typeshift puts the app's
// composite name there ("lmstudio/qwen/qwen3.8-27b"), which no server knows. Without this wrapper every request
// fails with "model not found". The spans still show the composite name as gen_ai.request.model.
public sealed class WireModelChatClient(IChatClient inner, string model) : DelegatingChatClient(inner)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        base.GetResponseAsync(messages, OnTheWire(options), cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        base.GetStreamingResponseAsync(messages, OnTheWire(options), cancellationToken);

    private ChatOptions? OnTheWire(ChatOptions? options)
    {
        if (options is null) return null;

        var request = options.Clone();
        request.ModelId = model;
        return request;
    }
}
