using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Hosting;

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
        var chat = support.WithConversationId(conversationId);

        await chat.RunAsync("Where is my refund?");
        await chat.RunAsync("When will it arrive?");
        // end-snippet

        // begin-snippet: load-conversation
        var entries = await support.WithConversationId(conversationId).LoadConversationAsync(ct);

        Console.WriteLine($"{entries.Count} entries, {entries.Sum(entry => entry.Usage?.TotalTokenCount)} tokens");
        // end-snippet

        if (entries.Count != 6) throw new InvalidOperationException("Expected two runs of three entries each.");

        // begin-snippet: live-sessions
        var tracker = provider.GetRequiredService<ConversationTracker<int>>();

        foreach (var run in tracker.Runs)
            Console.WriteLine($"{run.Agent} is answering conversation {run.Conversation} on {run.Model}");
        // end-snippet

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var watcher = WatchAsync(tracker, conversationId, watching.Token);
        var runWatcher = WatchRunsAsync(tracker, watching.Token);

        await foreach (var update in chat.WithSettingsId(settingsId).RunStreamingAsync("Thanks!", ct))
            Console.Write(update.Text);

        Console.WriteLine();
        await watcher;
        await watching.CancelAsync();
        await runWatcher;

        // begin-snippet: steering
        var input = "Send me the invoice too.";

        while (!tracker.TrySteer(conversationId, input))
        {
            try
            {
                await chat.WithSettingsId(settingsId).RunAsync(input);
                break;
            }
            catch (ConversationBusyException)
            {
                // A run of the conversation began in between and takes the input now
                continue;
            }
        }
        // end-snippet

        // A run cut off by a crash: its input is stored, nothing else
        var interrupted = 12;
        var question = new ChatMessage(ChatRole.User, "Where is my refund?");
        var entry = new ConversationEntry
        {
            Agent = "support_agent",
            Messages = [question],
            At = TimeProvider.System.GetUtcNow()
        };
        await provider.GetRequiredService<IConversationStore<int>>().AppendConversationAsync(interrupted, entry, ct);

        // begin-snippet: resume
        var resumed = await support.WithSettingsId(settingsId).WithConversationId(interrupted).ResumeAsync();
        // end-snippet

        if (resumed.Text.Length == 0) throw new InvalidOperationException("Expected the resumed run to answer.");

        var refunds = support.WithSettingsId(settingsId).WithConversationId(17);
        var message = CannedChatClient.Unavailable;

        // begin-snippet: retry
        try
        {
            await refunds.RunAsync(message);
        }
        catch (HttpRequestException)
        {
            // The conversation holds the message already, and the retry sends it once more
            await refunds.RetryAsync();
        }
        // end-snippet

        var stored = (await refunds.LoadConversationAsync()).SelectMany(entry => entry.Messages);
        if (stored.Count(sent => sent.Role == ChatRole.User) != 1)
            throw new InvalidOperationException("Expected the retried message in the conversation once.");
    }

    // Watches until the run ends, as a page that waits for one answer would
    private static async Task WatchAsync(ConversationTracker<int> tracker, int conversationId, CancellationToken ct)
    {
        // begin-snippet: watch-conversation
        await foreach (var update in tracker.WatchAsync(conversationId, ct))
        {
            Console.Write(update.Text);

            if (update.EndsRun) break;
        }
        // end-snippet
    }

    private static async Task WatchRunsAsync(ConversationTracker<int> tracker, CancellationToken ct)
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
