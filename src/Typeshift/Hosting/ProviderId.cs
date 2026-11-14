using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D77: a provider with its type, made where the type is known, in WithModel and in the generated code that applies a
// settings layer. It asks the IChatClientProvider of its type through a generic call, so agents of several provider
// types serve one app and no type is made at run time
internal abstract record ProviderId
{
    public abstract object Value { get; }

    // The interface that serves the provider, for the message of a run no client serves
    public abstract string Contract { get; }

    // D63: whether an IChatClientProvider of the provider's type is registered, which then alone serves it
    public abstract bool HasChatClientProvider(IServiceProvider services);

    public abstract ValueTask<IChatClient> GetChatClientAsync(
        IServiceProvider services,
        string model,
        CancellationToken ct);

    public sealed override string ToString() => Value.ToString() ?? "";
}

internal sealed record ProviderId<TProviderId>(TProviderId Id) : ProviderId where TProviderId : notnull
{
    public override object Value => Id;

    public override string Contract => $"IChatClientProvider<{TypeNames.Of<TProviderId>()}>";

    public override bool HasChatClientProvider(IServiceProvider services) =>
        services.GetService<IChatClientProvider<TProviderId>>() is not null;

    public override async ValueTask<IChatClient> GetChatClientAsync(
        IServiceProvider services,
        string model,
        CancellationToken ct) =>
        await services.GetRequiredService<IChatClientProvider<TProviderId>>().GetChatClientAsync(Id, model, ct)
        ?? throw new InvalidOperationException($"The {Contract} returned no client for '{model}' of '{Id}'.");
}
