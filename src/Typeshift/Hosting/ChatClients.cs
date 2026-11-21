using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D63, D86, D88: a run that names a key takes the client registered under it, and one handed a document the client
// its connector built, which Typeshift keeps. A run that names none takes the default provider: the key
// WithDefaultProvider names, or else the client registered for every agent. A provider nothing serves throws: the
// default serves no run that names one. A run without a model takes the model of the client that serves it, and
// throws when the client reports none
internal static class ChatClients
{
    // What runs is the agent's run or its summary, for the message of a provider and model nothing serves. The hold
    // keeps a document's client alive while the run uses it; null for a client the app registered
    public static async ValueTask<(IChatClient Client, IDisposable? Hold)> GetAsync(
        IServiceProvider services,
        string what,
        ProviderId? provider,
        string? model,
        CancellationToken ct)
    {
        var (client, hold) = provider is ProviderDocument { Provider: var document }
            ? await services.GetRequiredService<ProviderClients>().GetAsync(document, services, what, ct)
                .ConfigureAwait(false)
            : (GetRegistered(services, what, provider, model), null);

        // D63: a model nobody named and the client does not know would reach the provider as no model at all
        if (model is null && client.GetService<ChatClientMetadata>()?.DefaultModelId is null)
        {
            hold?.Dispose();
            throw NoModel(what, provider);
        }

        return (client, hold);
    }

    // D32: a summary's client is asked for when the history is compacted, and whether anything serves it is known
    // when the run starts; D87: so are those of the agents a run may reach. A document is served once a connector is
    public static void EnsureServed(IServiceProvider services, string what, ProviderId? provider, string? model)
    {
        if (provider is not ProviderDocument) GetRegistered(services, what, provider, model);
    }

    // D63: the key WithDefaultProvider names, which a run that names no provider takes; null for the client
    // registered for every agent
    public static ProviderId? DefaultProvider(IServiceProvider services) =>
        services.GetService<TypeshiftOptions>()?.DefaultProvider;

    // Keyed services take keys of any type, so a provider of any type finds its client
    private static IChatClient GetRegistered(
        IServiceProvider services,
        string what,
        ProviderId? provider,
        string? model) =>
        provider is null
            ? services.GetService<IChatClient>() ?? throw NoDefault(what, model)
            : services.GetKeyedService<IChatClient>(provider.Value) ?? throw NotServed(services, what, provider, model);

    private static InvalidOperationException NoDefault(string what, string? model) =>
        new(
            $"No chat client serves {what}{(model is null ? "" : $" on '{model}'")}, which names no provider, and no "
            + "default provider is registered: register a client for every agent with services.AddChatClient(client) "
            + "or TypeshiftRuntime.Create(client), or name a registered one with WithDefaultProvider(key). Or name the "
            + "run's provider with host.WithProvider(provider), WithChatOptions().WithProvider(provider) or in the "
            + "agent's settings.");

    // The registered keys tell a typo from a provider the app never registered
    private static InvalidOperationException NotServed(
        IServiceProvider services,
        string what,
        ProviderId provider,
        string? model)
    {
        var named = Equals(provider, DefaultProvider(services))
            ? ", the default provider WithDefaultProvider names"
            : "";

        return new(
            $"No chat client serves {what} on {(model is null ? "" : $"'{model}' of ")}'{provider}'{named}: "
            + $"{services.GetRequiredService<RegisteredChatClients>().Describe()}. Name a provider that has a client, "
            + "register one with services.AddKeyedChatClient(provider, client), or hand the run a provider document "
            + "with host.WithProvider(document). The default provider serves only a run that names no provider.");
    }

    private static InvalidOperationException NoModel(string what, ProviderId? provider) =>
        new(
            $"No model is named for {what}{(provider is null ? "" : $" on '{provider}'")}, and the client that serves "
            + "it names no model of its own in its ChatClientMetadata. Name a model with host.WithModel(model), "
            + "WithChatOptions().WithModel(model), in the agent's settings or in a provider document's Model, or build "
            + "the client with a model.");
}

// D63: the keys the app registered chat clients under, for the message of a run whose provider nothing serves. They
// are read from the registrations, since a container does not list its keys; a factory for any key names none
internal sealed class RegisteredChatClients(IServiceCollection registrations)
{
    public string Describe()
    {
        var keys = registrations
            .Where(registration => registration is { IsKeyedService: true, ServiceKey: not null }
                && registration.ServiceType == typeof(IChatClient)
                && !Equals(registration.ServiceKey, KeyedService.AnyKey))
            .Select(registration => $"'{registration.ServiceKey}'")
            .Distinct()
            .ToList();

        return keys.Count == 0
            ? "no client is registered under a provider"
            : $"the registered clients are under {string.Join(", ", keys)}";
    }
}
