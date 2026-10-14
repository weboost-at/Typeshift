using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Scenarios;

internal static class Conversations
{
    public static async Task RunAsync(IServiceProvider provider, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();

        // begin-snippet: conversation
        var conversationId = 7;

        await support.RunAsync("Where is my refund?", conversationId: conversationId);
        await support.RunAsync("When will it arrive?", conversationId: conversationId);
        // end-snippet

        // begin-snippet: load-conversation
        var store = provider.GetRequiredService<ITypeshiftStore>();
        var sessions = await store.LoadConversationAsync(conversationId, ct);

        Console.WriteLine($"{sessions.Count} sessions, {sessions.Sum(s => s.Usage?.TotalTokenCount)} tokens");
        // end-snippet

        // begin-snippet: live-sessions
        var tracker = provider.GetRequiredService<ConversationTracker>();

        foreach (var session in tracker.Sessions)
            Console.WriteLine($"{session.Agent} is answering conversation {session.Conversation} on {session.Model}");
        // end-snippet

        // begin-snippet: watch-conversation
        await foreach (var update in tracker.WatchAsync(conversationId, ct))
            Console.Write(update.Text);
        // end-snippet
    }
}
