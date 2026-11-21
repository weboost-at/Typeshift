using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D63, D86: a run that names a provider takes that provider's client, from the IChatClientProvider of the provider's
// type or else the client registered under the provider. A run that names none takes the default provider: the key
// WithDefaultProvider names, or else the client registered for every agent. A provider nothing serves throws: the
// default serves no run that names one. A run without a model takes the model of the client that serves it, and
// throws when the client reports none
internal static class ChatClients
{
    // What runs is the agent's run or its summary, for the message of a provider and model nothing serves
    public static async ValueTask<IChatClient> GetAsync(
        IServiceProvider services,
        string what,
        ProviderId? provider,
        string? model,
        CancellationToken ct)
    {
        var client = GetRegistered(services, what, provider, model)
            ?? await provider!.GetChatClientAsync(services, model!, ct).ConfigureAwait(false);

        // D63: a model nobody named and the client does not know would reach the provider as no model at all
        if (model is null && client.GetService<ChatClientMetadata>()?.DefaultModelId is null)
            throw NoModel(what, provider);

        return client;
    }

    // D32: a summary's client is asked for when the history is compacted, and whether anything serves it is known
    // when the run starts; D87: so are those of the agents a run may reach
    public static void EnsureServed(IServiceProvider services, string what, ProviderId? provider, string? model) =>
        GetRegistered(services, what, provider, model);

    // D63: the key WithDefaultProvider names, which a run that names no provider takes; null for the client
    // registered for every agent
    public static ProviderId? DefaultProvider(IServiceProvider services) =>
        services.GetService<TypeshiftOptions>()?.DefaultProvider;

    // The registered client that serves the run, or null where the IChatClientProvider of the provider's type does;
    // keyed services take keys of any type, so a provider of any type finds its client
    private static IChatClient? GetRegistered(
        IServiceProvider services,
        string what,
        ProviderId? provider,
        string? model)
    {
        if (provider is null) return services.GetService<IChatClient>() ?? throw NoDefault(what, model);

        if (provider.HasChatClientProvider(services))
            return model is null ? throw NoModelForProviderClass(what, provider) : null;

        return services.GetKeyedService<IChatClient>(provider.Value)
            ?? throw NotServed(services, what, provider, model);
    }

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
            + $"{services.GetRequiredService<RegisteredChatClients>().Describe()}, and no {provider.Contract} is "
            + "registered. Name a provider that has a client, register one with "
            + "services.AddKeyedChatClient(provider, client), or register a provider class with "
            + "WithChatClientProvider<T>(). The default provider serves only a run that names no provider.");
    }

    private static InvalidOperationException NoModelForProviderClass(string what, ProviderId provider) =>
        new(
            $"No chat client serves {what} on '{provider}', which names no model: the {provider.Contract} that is "
            + "registered serves a provider and a model. Name the model too, in the agent's code or its settings.");

    private static InvalidOperationException NoModel(string what, ProviderId? provider) =>
        new(
            $"No model is named for {what}{(provider is null ? "" : $" on '{provider}'")}, and the client that serves "
            + "it names no model of its own in its ChatClientMetadata. Name a model with host.WithModel(model), "
            + "WithChatOptions().WithModel(model) or in the agent's settings, or build the client with a model.");
}

// D63: the keys the app registered chat clients under, for the message of a run whose provider nothing serves. They
// are read from the registrations, since a container does not list its keys
internal sealed class RegisteredChatClients(IServiceCollection registrations)
{
    public string Describe()
    {
        var keys = registrations
            .Where(registration => registration.IsKeyedService && registration.ServiceType == typeof(IChatClient))
            .Select(registration => $"'{registration.ServiceKey}'")
            .Distinct()
            .ToList();

        return keys.Count == 0
            ? "no client is registered under a provider"
            : $"the registered clients are under {string.Join(", ", keys)}";
    }
}
