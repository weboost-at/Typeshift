using System.ClientModel;
using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using Typeshift.Demos.Hosting;

namespace Typeshift.Demos.Providers;

/// <summary>
/// The canned models of a demo by name, for runs without a network.
/// </summary>
public sealed class CannedModels : Dictionary<string, IChatClient>;

/// <summary>
/// One client under each provider the user defined, keyed by its name, which a stored setting names: the container
/// builds it when a run first asks for it and disposes it with the app, and it serves every model of its provider,
/// since a run sends its model with each request.
/// </summary>
public static class CatalogClients
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddCatalogClients(ProviderCatalog catalog, CannedModels canned)
        {
            foreach (var definition in catalog.Providers)
            {
                services.AddKeyedSingleton<IChatClient>(definition.Name, (_, _) => Create(definition, canned));
            }

            return services;
        }
    }

    private static IChatClient Create(ProviderDefinition provider, CannedModels canned)
    {
        var key = ProviderCatalog.Key(provider);

        return provider.Kind switch
        {
            "openai" => OpenAi(provider, key).GetChatClient("default").AsIChatClient(),
            "anthropic" => new AnthropicClient { ApiKey = key }.AsIChatClient(),
            "canned" => new CannedProvider(canned),
            _ => throw new DemoException(
                $"The provider kind '{provider.Kind}' is unknown: use openai, anthropic or canned.")
        };
    }

    private static OpenAIClient OpenAi(ProviderDefinition provider, string key) =>
        provider.Endpoint is { Length: > 0 } endpoint
            ? new OpenAIClient(new ApiKeyCredential(key.Length > 0 ? key : "none"), new OpenAIClientOptions
            {
                Endpoint = new Uri(endpoint)
            })
            : new OpenAIClient(new ApiKeyCredential(key));
}

// The canned provider, whose models are the demo's canned clients: a run's model picks one
internal sealed class CannedProvider(CannedModels models) : IChatClient
{
    private static readonly ChatClientMetadata Metadata = new("canned");

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Model(options).GetResponseAsync(messages, options, cancellationToken);

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Model(options).GetStreamingResponseAsync(messages, options, cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(ChatClientMetadata) ? Metadata : null;

    // The canned models belong to the demo, which outlives the provider
    public void Dispose()
    {
    }

    // CLUNKY 5: a mistyped model fails here, in the middle of a run, long after the settings UI saved it; nothing
    // validates a model name when UpdateSettingsAsync stores it
    private IChatClient Model(ChatOptions? options) =>
        options?.ModelId is { } model && models.TryGetValue(model, out var client)
            ? client
            : throw new DemoException($"No canned model '{options?.ModelId}' exists in this demo.");
}
