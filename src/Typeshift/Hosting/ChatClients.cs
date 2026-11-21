using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D63, D86: a run that names a provider takes that provider's client, from the IChatClientProvider of the provider's
// type or else the client registered under the provider. A run that names none takes the default client, the one
// registered for every agent. A provider nothing serves throws: the default serves no run that names one
internal static class ChatClients
{
    // What runs is the agent's run or its summary, for the message of a provider and model nothing serves
    public static async ValueTask<IChatClient> GetAsync(
        IServiceProvider services,
        string what,
        ProviderId? provider,
        string? model,
        CancellationToken ct) =>
        GetRegistered(services, what, provider, model)
        ?? await provider!.GetChatClientAsync(services, model!, ct).ConfigureAwait(false);

    // D32: a summary's client is asked for when the history is compacted, and whether anything serves it is known
    // when the run starts
    public static void EnsureServed(IServiceProvider services, string what, ProviderId? provider, string? model) =>
        GetRegistered(services, what, provider, model);

    // The registered client that serves the run, or null where the IChatClientProvider of the provider's type does;
    // keyed services take keys of any type, so a provider of any type finds its client
    private static IChatClient? GetRegistered(
        IServiceProvider services,
        string what,
        ProviderId? provider,
        string? model)
    {
        if (provider is null) return services.GetService<IChatClient>() ?? throw NoDefault(what, model);

        if (provider.HasChatClientProvider(services)) return model is null ? throw NoModel(what, provider) : null;

        return services.GetKeyedService<IChatClient>(provider.Value)
            ?? throw NotServed(services, what, provider, model);
    }

    private static InvalidOperationException NoDefault(string what, string? model) =>
        new(
            $"No chat client serves {what}{(model is null ? "" : $" on '{model}'")}, which names no provider, and no "
            + "default client is registered: register one for every agent with services.AddChatClient(client) or "
            + "TypeshiftRuntime.Create(client), or name a provider with WithChatOptions().WithProvider(provider) or "
            + "in the agent's settings.");

    // The registered keys tell a typo from a provider the app never registered
    private static InvalidOperationException NotServed(
        IServiceProvider services,
        string what,
        ProviderId provider,
        string? model) =>
        new(
            $"No chat client serves {what} on {(model is null ? "" : $"'{model}' of ")}'{provider}': "
            + $"{services.GetRequiredService<RegisteredChatClients>().Describe()}, and no {provider.Contract} is "
            + "registered. Name a provider that has a client, register one with "
            + "services.AddKeyedChatClient(provider, client), or register a provider class with "
            + "WithChatClientProvider<T>(). The default client serves only a run that names no provider, as an agent "
            + "without WithChatOptions().WithProvider(provider) does.");

    private static InvalidOperationException NoModel(string what, ProviderId provider) =>
        new(
            $"No chat client serves {what} on '{provider}', which names no model: the {provider.Contract} that is "
            + "registered serves a provider and a model. Name the model too, in the agent's code or its settings.");
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
