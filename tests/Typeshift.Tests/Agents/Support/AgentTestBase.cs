using Typeshift.Tests.Tools.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Agents.Support;

// The host's registration, with a store in memory, live updates, and every model served by the scripted client
public abstract class AgentTestBase : HostTestBase
{
    protected ProvidedModels Models { get; } = [];

    protected AgentHost<TAgent> Host<TAgent>() where TAgent : Agent =>
        Scope.GetRequiredService<AgentHost<TAgent>>();

    protected ConversationTracker Tracker => Services.GetRequiredService<ConversationTracker>();

    protected Task<IReadOnlyList<ConversationEntry>> EntriesAsync(Guid conversation) =>
        Services.GetRequiredService<ITypeshiftStore>().LoadConversationAsync(conversation, Ct);

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddTypeshift()
            .WithStore<InMemoryTypeshiftStore>()
            .WithChatClientProvider<ScriptedChatClientProvider>()
            .WithRealtimeTracking();

        services.AddSingleton(ChatClient);
        services.AddSingleton(Models);
        services.AddScoped<CallScope>();
    }
}
