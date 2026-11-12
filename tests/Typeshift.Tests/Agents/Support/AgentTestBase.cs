using System.Text.Json;
using Typeshift.Tests.Tools.Support;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Agents.Support;

// The host's registration, with a store in memory and every model served by the scripted client
public abstract class AgentTestBase : HostTestBase
{
    // About a thousand tokens of text, at four characters to a token
    protected const int Page = 4_000;

    protected ProvidedModels Models { get; } = [];

    protected AgentHost<TAgent> Host<TAgent>() where TAgent : Agent =>
        Scope.GetRequiredService<AgentHost<TAgent>>();

    protected TypeshiftRuntime Runtime => Services.GetRequiredService<TypeshiftRuntime>();

    protected ConversationTracker<Guid> Tracker => Services.GetRequiredService<ConversationTracker<Guid>>();

    protected Task<IReadOnlyList<ConversationEntry>> EntriesAsync(Guid conversation) =>
        Services.GetRequiredService<ITypeshiftStore<Guid>>().LoadConversationAsync(conversation, Ct);

    // The JSON schema the run's first model call asked the answer to match
    protected JsonElement ResponseSchema() =>
        ((ChatResponseFormatJson)ChatClient.Options[0]!.ResponseFormat!).Schema!.Value;

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddTypeshift()
            .WithStore<InMemoryTypeshiftStore<Guid>>()
            .WithChatClientProvider<ScriptedChatClientProvider>();

        services.AddSingleton(ChatClient);
        services.AddSingleton(Models);
        services.AddScoped<CallScope>();
    }
}
