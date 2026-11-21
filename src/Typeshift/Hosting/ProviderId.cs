using System.Globalization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D86: a provider with its type, made where the type is known, in WithProvider and in the generated code that
// applies a settings layer. It asks the IChatClientProvider of its type through a generic call, so agents of several
// provider types serve one app and no type is made at run time
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

    // The provider as a span and a conversation entry name it, the same whatever the culture
    public sealed override string ToString() => Convert.ToString(Value, CultureInfo.InvariantCulture) ?? "";
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
        await services
            .GetRequiredService<IChatClientProvider<TProviderId>>()
            .GetChatClientAsync(Id, model, ct)
            .ConfigureAwait(false)
        ?? throw new InvalidOperationException($"The {Contract} returned no client for '{model}' of '{Id}'.");
}
