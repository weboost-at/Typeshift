using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Scenarios;

internal static class Conversations
{
    public static async Task RunAsync(IServiceProvider provider, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();
        var settingsId = 42;

        // begin-snippet: conversation
        var conversationId = 7;

        await support.RunAsync("Where is my refund?", conversationId: conversationId);
        await support.RunAsync("When will it arrive?", conversationId: conversationId);
        // end-snippet

        // begin-snippet: load-conversation
        var store = provider.GetRequiredService<IConversationStore>();
        var entries = await store.LoadConversationAsync(conversationId, ct);

        Console.WriteLine($"{entries.Count} entries, {entries.Sum(entry => entry.Usage?.TotalTokenCount)} tokens");
        // end-snippet

        if (entries.Count != 6) throw new InvalidOperationException("Expected two runs of three entries each.");

        // begin-snippet: live-sessions
        var tracker = provider.GetRequiredService<ConversationTracker>();

        foreach (var run in tracker.Runs)
            Console.WriteLine($"{run.Agent} is answering conversation {run.Conversation} on {run.Model}");
        // end-snippet

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var watcher = WatchAsync(tracker, conversationId, watching.Token);
        var runWatcher = WatchRunsAsync(tracker, watching.Token);

        await foreach (var update in support.RunStreamingAsync("Thanks!", settingsId, conversationId, ct: ct))
            Console.Write(update.Text);

        Console.WriteLine();
        await watching.CancelAsync();
        await watcher;
        await runWatcher;

        // begin-snippet: steering
        var input = "Send me the invoice too.";

        if (!tracker.TrySteer(conversationId, input))
            await support.RunAsync(input, settingsId, conversationId);
        // end-snippet

        // A run cut off by a crash: its input is stored, nothing else
        var interrupted = 12;
        var question = new ChatMessage(ChatRole.User, "Where is my refund?");
        var entry = new ConversationEntry("support_agent", [question], TimeProvider.System.GetUtcNow());
        await store.AppendConversationAsync(interrupted, entry, ct);

        // begin-snippet: resume
        var resumed = await support.ResumeAsync(interrupted, settingsId);
        // end-snippet

        if (resumed.Text.Length == 0) throw new InvalidOperationException("Expected the resumed run to answer.");
    }

    // Watches until the scenario stops it, as a UI would until its user leaves
    private static async Task WatchAsync(ConversationTracker tracker, int conversationId, CancellationToken ct)
    {
        try
        {
            // begin-snippet: watch-conversation
            await foreach (var update in tracker.WatchAsync(conversationId, ct))
                Console.Write(update.Text);
            // end-snippet
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Console.WriteLine();
        }
    }

    private static async Task WatchRunsAsync(ConversationTracker tracker, CancellationToken ct)
    {
        try
        {
            // begin-snippet: watch-runs
            await foreach (var run in tracker.WatchRunsAsync(ct))
                Console.WriteLine($"Conversation {run.Conversation} is {(run.Ended ? "idle" : "running")}");
            // end-snippet
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Console.WriteLine("Stopped watching runs.");
        }
    }
}
