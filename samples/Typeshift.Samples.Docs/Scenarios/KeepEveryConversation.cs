using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Scenarios;

// Every conversation kept, on services of its own and the memory without a store: a run without an id starts a
// conversation, the next run continues it by the id its response names, and ids of the app's own type come from
// NewConversationIds
internal static class KeepEveryConversation
{
    public static async Task RunAsync(IChatClient client)
    {
        var services = new ServiceCollection();

        // begin-snippet: persist-all-conversations
        services.AddTypeshift()
            .WithOptions(options => options.PersistAllConversations = true);
        // end-snippet

        services.AddChatClient(client);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var assistant = scope.ServiceProvider.GetRequiredService<AgentHost<AssistantAgent>>();

        // begin-snippet: persist-all-conversations-run
        var first = await assistant.RunAsync("Where is my refund?");
        var chat = assistant.WithConversationId(first.GetConversationId<Guid>());

        var next = await chat.RunAsync("When will it arrive?");
        // end-snippet

        var entries = await chat.LoadConversationAsync();
        Expect(entries.Count == 6, "the next run to continue the conversation the first one started");
        Expect(next.ConversationId == first.ConversationId, "the next run's response to name the same conversation");

        await NewConversationIdsAsync(client);
    }

    private static async Task NewConversationIdsAsync(IChatClient client)
    {
        var services = new ServiceCollection();

        // begin-snippet: new-conversation-ids
        services.AddTypeshift()
            .WithOptions(options =>
            {
                options.PersistAllConversations = true;
                options.NewConversationIds(() => $"chat-{Guid.CreateVersion7():N}");
            });
        // end-snippet

        services.AddChatClient(client);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var assistant = scope.ServiceProvider.GetRequiredService<AgentHost<AssistantAgent>>();

        var first = await assistant.RunAsync("Where is my refund?");
        Expect(first.ConversationId?.StartsWith("chat-") is true, "a new conversation under an id of the app's own");

        var stored = await assistant.WithConversationId(first.ConversationId!).LoadConversationAsync();
        Expect(stored.Count == 3, "the run stored under a string id");
    }

    private static void Expect(bool condition, string what)
    {
        if (!condition) throw new InvalidOperationException($"Expected {what}.");
    }
}
