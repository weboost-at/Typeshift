using Agentic.Agents;
using Agentic.Conversations;
using Agentic.Persistence;
using Agentic.Samples.Docs.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Samples.Docs.Scenarios;

internal static class Conversations
{
    public static async Task RunAsync()
    {
        await using var services = Host.Create();
        var support = services.GetRequiredService<AgentRuntime<SupportAgent>>();
        var customer = Guid.NewGuid();
        var running = 0;

        // begin-snippet: live-sessions
        var tracker = services.GetRequiredService<ConversationTracker>();

        tracker.OnConversationsChanged += ct =>
        {
            foreach (var session in tracker.Sessions)
                Console.WriteLine($"{session.ExecutionContext.Value} is running on {session.Model}");

            return Task.CompletedTask;
        };
        // end-snippet

        tracker.OnConversationsChanged += ct =>
        {
            running += tracker.Sessions.Count;
            return Task.CompletedTask;
        };

        // begin-snippet: conversation
        var conversation = Guid.NewGuid();

        var options = await support.ResolveChatOptionsAsync(customer, conversationId: conversation.ToString());
        var client = await support.CreateChatClientAsync(options);

        await client.GetResponseAsync("Where is my refund?", options);
        await client.GetResponseAsync("When will it arrive?", options);
        // end-snippet

        // begin-snippet: stored-conversation
        var store = services.GetRequiredService<IAgenticStore<Guid>>();
        var stored = await store.LoadAsync<StoredConversation>(conversation, CancellationToken.None);

        Console.WriteLine($"{stored!.Sessions.Count} sessions, {stored.AggregatedUsage.TotalTokenCount} tokens");
        // end-snippet

        Expect.That(stored.Sessions.Count == 2, "the second call resumes the conversation");
        Expect.That(running == 2, "live sessions are listed while they run");
    }
}
