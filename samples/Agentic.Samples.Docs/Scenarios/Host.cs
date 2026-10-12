using Agentic.Agents;
using Agentic.Json;
using Agentic.Persistence.InMemory;
using Agentic.Samples.Docs.Chat;
using Agentic.Samples.Docs.Tickets;
using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Samples.Docs.Scenarios;

internal static class Host
{
    public static ServiceProvider Create(Action<IServiceCollection>? configure = null)
    {
        // begin-snippet: registration
        var services = new ServiceCollection();

        services.AddAgentic();
        services.AddAgenticStore<InMemoryAgenticStore<Guid>, Guid>();

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
