using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D63, D77: the IChatClientProvider of the provider's type serves a provider and a model, then a client keyed by the
// provider, then the client registered for every agent
internal static class ChatClients
{
    // Null when none serves the run; keyed services take keys of any type, so a provider of any type finds its client
    public static async ValueTask<IChatClient?> GetAsync(
        IServiceProvider services,
        ProviderId? provider,
        string? model,
        CancellationToken ct)
    {
        var provided = provider is null || model is null
            ? null
            : await provider.GetChatClientAsync(services, model, ct);

        return provided
            ?? (provider is null ? null : services.GetKeyedService<IChatClient>(provider.Value))
            ?? services.GetService<IChatClient>();
    }

    // What runs is the agent's run or its summary; each message names what would serve it
    public static string NoClient(string what, ProviderId? provider, string? model) =>
        (provider, model) switch
        {
            (null, null) => $"No chat client serves {what}, which names no model: register one for every agent with "
                + "services.AddChatClient(client), or name a model with WithChatOptions().WithModel(provider, model).",
            (null, _) => $"No chat client serves {what} on '{model}', which names no provider: register one for every "
                + "agent with services.AddChatClient(client), or name the model's provider too.",
            _ => $"No chat client serves {what} on {(model is null ? "" : $"'{model}' of ")}'{provider}': register "
                + "one under the provider with services.AddKeyedChatClient(provider, client), one for every agent "
                + $"with services.AddChatClient(client), or an {provider.Contract} with WithChatClientProvider<T>()."
        };
}
