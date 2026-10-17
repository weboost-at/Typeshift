using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D56: the library holds providers boxed; this closes the call over the build's provider id type
internal abstract class ChatClients
{
    public abstract ValueTask<IChatClient> GetAsync(
        IServiceProvider services,
        object provider,
        string model,
        CancellationToken ct);
}

internal sealed class ChatClients<TProviderId> : ChatClients where TProviderId : notnull
{
    public override ValueTask<IChatClient> GetAsync(
        IServiceProvider services,
        object provider,
        string model,
        CancellationToken ct)
    {
        var clients = services.GetService<IChatClientProvider<TProviderId>>()
            ?? throw new InvalidOperationException(
                "No IChatClientProvider is registered: call WithChatClientProvider<T>() after AddTypeshift().");

        return provider is TProviderId id
            ? clients.GetChatClientAsync(id, model, ct)
            : throw new InvalidOperationException(
                $"The provider '{provider}' is a {provider.GetType().Name}, not a {typeof(TProviderId).Name}, the "
                + "build's provider id type: build every project with the same TypeshiftProviderIdType.");
    }
}
