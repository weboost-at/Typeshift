using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Hosting;
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

        await using (var request = provider.CreateAsyncScope())
        {
            var desk = request.ServiceProvider.GetRequiredService<RefundDesk>();
            var injected = await desk.AnswerAsync("Where is my refund?", CancellationToken.None);
            Expect(injected == answer.Text, "a host injected into a service answers");
        }

        var digest = new TicketDigest(provider.GetRequiredService<TypeshiftRuntime>(), TimeProvider.System);
        var digested = await digest.DigestAsync(CancellationToken.None);
        Expect(digested.Length > 0, "a background job answers from a scope of its own");

        // begin-snippet: run-with-settings-id
        var settingsId = 42;

        var stored = await support.WithSettingsId(settingsId).RunAsync("Where is my refund?");
        // end-snippet

        Expect(stored.Text == answer.Text, "a run with a settings id answers");

        // begin-snippet: run-settings
        var run = new SupportAgent.Settings
        {
            Provider = "anthropic",
            Model = "claude-sonnet-5-5",
            Temperature = 0,
            SearchTickets = new TicketToolset.SearchTicketsOverrides { IncludeClosed = true },
            SearchArticles = new KnowledgeToolset.SearchArticlesOverrides { Language = "de" }
        };

        var thorough = await support.WithSettingsId(settingsId).RunAsync("Where is my refund?", settings: run);
        // end-snippet

        Expect(thorough.Text.Length > 0, "a run with settings answers");

        // begin-snippet: structured-output
        var report = await support.WithSettingsId(settingsId).RunAsync<RefundStatus>("Summarize the refund.");

        Console.WriteLine($"{report.Result.Invoice}: {report.Result.State}");
        // end-snippet

        Expect(report.Result.State == RefundState.Approved, "structured output is parsed");

        // begin-snippet: structured-streaming
        var summary = support.WithSettingsId(settingsId).RunStreamingAsync<RefundStatus>("Summarize the refund.");

        await foreach (var update in summary)
            Console.Write(update.Text);

        Console.WriteLine($" ({summary.Response.Result.State})");
        // end-snippet

        Expect(summary.Response.Result.State == RefundState.Approved, "streamed structured output is parsed");

        // begin-snippet: streaming
        await foreach (var update in support.WithSettingsId(settingsId).RunStreamingAsync("Where is my refund?"))
            Console.Write(update.Text);
        // end-snippet

        Console.WriteLine();

        // begin-snippet: raw-path
        var options = await support.WithSettingsId(settingsId).ResolveOptionsAsync(settings: run);

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
