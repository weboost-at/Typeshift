using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Knowledge;
using Typeshift.Samples.Tickets;

namespace Typeshift.Samples.Scenarios;

internal static class RunningAgents
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        // begin-snippet: run
        await using var scope = provider.CreateAsyncScope();
        var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();

        var answer = await support.RunAsync("Where is my refund?");

        Console.WriteLine($"{answer.Messages[^1].AuthorName}: {answer.Text}");
        // end-snippet

        // begin-snippet: run-with-settings-id
        var settingsId = 42;

        var stored = await support.RunAsync("Where is my refund?", settingsId);
        // end-snippet

        Expect(stored.Text == answer.Text, "a run with a settings id answers");

        // begin-snippet: run-settings
        var run = new SupportAgent.Settings
        {
            Model = "reasoning-model",
            Temperature = 0,
            SearchTickets = new TicketToolset.SearchTicketsOverrides { IncludeClosed = true },
            SearchArticles = new KnowledgeToolset.SearchArticlesOverrides { Language = "de" }
        };

        var thorough = await support.RunAsync("Where is my refund?", settingsId, run);
        // end-snippet

        Expect(thorough.Text.Length > 0, "a run with settings answers");

        // begin-snippet: structured-output
        var report = await support.RunAsync<RefundStatus>("Summarize the refund.", settingsId);

        Console.WriteLine($"{report.Result.Invoice}: {report.Result.State}");
        // end-snippet

        Expect(report.Result.State == RefundState.Approved, "structured output is parsed");

        // begin-snippet: streaming
        await foreach (var update in support.RunStreamingAsync("Where is my refund?", settingsId))
            Console.Write(update.Text);
        // end-snippet

        Console.WriteLine();

        // begin-snippet: raw-path
        var options = await support.ResolveOptionsAsync(settingsId, run);

        using var client = await support.CreateChatClientAsync(options);
        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Where is my refund?")], options);
        // end-snippet

        Expect(options.Tools?.Count > 0, "resolved options carry the agent's tools");
        Expect(response.Text == answer.Text, "the raw path answers like a run");
    }

    private static void Expect(bool condition, string what)
    {
        if (!condition) throw new InvalidOperationException($"Expected {what}.");
    }
}

// begin-snippet: structured-output-type
internal sealed record RefundStatus(string Invoice, RefundState State, DateOnly? Expected);

internal enum RefundState
{
    Requested,
    Approved,
    Paid
}
// end-snippet
