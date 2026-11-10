using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D56: the library holds providers boxed; this closes the call over the build's provider id type
internal abstract class ChatClients
{
    // D63: the host's provider serves a provider and a model, then a client keyed by the provider, then the client
    // registered for every agent; null when none does
    public abstract ValueTask<IChatClient?> GetAsync(
        IServiceProvider services,
        object? provider,
        string? model,
        CancellationToken ct);

    // What runs is the agent's run or its summary; each message names what would serve it
    public static string NoClient(string what, object? provider, string? model) =>
        (provider, model) switch
        {
            (null, null) => $"No chat client serves {what}, which names no model: register one for every agent with "
                + "services.AddChatClient(client), or name a model with WithChatOptions().WithModel(provider, model).",
            (null, _) => $"No chat client serves {what} on '{model}', which names no provider: register one for every "
                + "agent with services.AddChatClient(client), or name the model's provider too.",
            _ => $"No chat client serves {what} on {(model is null ? "" : $"'{model}' of ")}'{provider}': register "
                + "one under the provider with services.AddKeyedChatClient(provider, client), one for every agent "
                + "with services.AddChatClient(client), or an IChatClientProvider with WithChatClientProvider<T>()."
        };
}

internal sealed class ChatClients<TProviderId> : ChatClients where TProviderId : notnull
{
    public override async ValueTask<IChatClient?> GetAsync(
        IServiceProvider services,
        object? provider,
        string? model,
        CancellationToken ct)
    {
        if (provider is not null && model is not null
            && services.GetService<IChatClientProvider<TProviderId>>() is { } clients)
        {
            return provider is TProviderId id
                ? await clients.GetChatClientAsync(id, model, ct)
                    ?? throw new InvalidOperationException(
                        $"The IChatClientProvider returned no client for '{model}' of '{provider}'.")
                : throw new InvalidOperationException(
                    $"The provider '{provider}' is a {provider.GetType().Name}, not a {typeof(TProviderId).Name}, the "
                    + "build's provider id type: build every project with the same TypeshiftProviderIdType.");
        }

        return (provider is null ? null : services.GetKeyedService<IChatClient>(provider))
            ?? services.GetService<IChatClient>();
    }
}
