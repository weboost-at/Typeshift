using Typeshift.Agents;
using Typeshift.Json;
using Typeshift.Persistence.InMemory;
using Typeshift.Samples.Docs.Chat;
using Typeshift.Samples.Docs.Tickets;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Samples.Docs.Scenarios;

internal static class Host
{
    public static ServiceProvider Create(Action<IServiceCollection>? configure = null)
    {
        // begin-snippet: registration
        var services = new ServiceCollection();

        services.AddTypeshift();
        services.AddTypeshiftStore<InMemoryTypeshiftStore<Guid>, Guid>();

        services.AddKeyedChatClient("fast-model", new CannedChatClient("fast-model"));
        services.AddKeyedChatClient("reasoning-model", new CannedChatClient("reasoning-model"));
        services.AddSingleton<IChatClientProvider, KeyedChatClientProvider>();
        // end-snippet

        services.AddSingleton<ITicketClient, InMemoryTicketClient>();
        services.Configure<AgentJsonOptions>(options => options.TypeInfoResolverChain.Add(SupportJsonContext.Default));
        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }
}
