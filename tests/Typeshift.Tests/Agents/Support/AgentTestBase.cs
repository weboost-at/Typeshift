using Typeshift.Tests.Tools.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Agents.Support;

// The host's registration, with a store in memory and every model served by the scripted client
public abstract class AgentTestBase : HostTestBase
{
    protected ProvidedModels Models { get; } = [];

    protected AgentHost<TAgent> Host<TAgent>() where TAgent : Agent =>
        Scope.GetRequiredService<AgentHost<TAgent>>();

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddTypeshift()
            .WithStore<InMemoryTypeshiftStore>()
            .WithChatClientProvider<ScriptedChatClientProvider>();

        services.AddSingleton(ChatClient);
        services.AddSingleton(Models);
        services.AddScoped<CallScope>();
    }
}
