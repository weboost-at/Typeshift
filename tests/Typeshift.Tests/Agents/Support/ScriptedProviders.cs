using Typeshift.Tests.Testing.Fakes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Agents.Support;

// Serves the providers a run names by a string, whatever the string, as an app's clients under its keys would; a key of
// another type stays unserved, unless a test registers a client under it
public static class ScriptedProviders
{
    extension(TypeshiftBuilder typeshift)
    {
        // The test's scripted client, which records the provider and the model of each model call
        public TypeshiftBuilder WithScriptedProviders() =>
            typeshift.WithClientOfEveryProvider((services, provider) => new RecordingChatClient(
                services.GetRequiredService<ScriptedChatClient>(),
                provider,
                services.GetRequiredService<ProvidedModels>()));

        // A client the factory makes each time a run asks for a provider's, as a provider of a client per run
        public TypeshiftBuilder WithClientOfEveryProvider(Func<IServiceProvider, string, IChatClient> create)
        {
            typeshift.Services.AddKeyedTransient<IChatClient>(
                KeyedService.AnyKey,
                (services, key) => key is string provider ? create(services, provider) : null!);

            return typeshift;
        }
    }
}

// A model call's provider and model, as the client under the provider's key saw it
public sealed class RecordingChatClient(IChatClient inner, object provider, ProvidedModels models)
    : DelegatingChatClient(inner)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        models.Add(new(provider, options?.ModelId));
        return base.GetResponseAsync(messages, options, cancellationToken);
    }

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        models.Add(new(provider, options?.ModelId));
        return base.GetStreamingResponseAsync(messages, options, cancellationToken);
    }

    // The test owns the scripted client
    protected override void Dispose(bool disposing)
    {
    }
}

public sealed class ProvidedModels : List<ProvidedModel>;

// A provider of any type, a string or an enum, compared by its value
public sealed record ProvidedModel(object Provider, string? Model);
