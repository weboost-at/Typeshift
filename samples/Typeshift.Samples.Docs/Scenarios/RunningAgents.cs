using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Hosting;
using Typeshift.Samples.Knowledge;
using Typeshift.Samples.Tickets;

namespace Typeshift.Samples.Scenarios;

internal static class RunningAgents
{
    public static async Task RunAsync(IServiceProvider provider, CancellationToken ct)
    {
        // begin-snippet: run
        await using var scope = provider.CreateAsyncScope();
        var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();

        var answer = await support.RunAsync("Where is my refund?");

        Console.WriteLine($"{answer.Messages[^1].AuthorName}: {answer.Text}");
        // end-snippet

        using (var leaving = new CancellationTokenSource())
        {
            await leaving.CancelAsync();

            var stopped = await ThrowsAsync<OperationCanceledException>(
                () => support.RunAsync("Where is my refund?", leaving.Token));

            Expect(stopped, "a cancelled token to stop the run");
        }

        // The bytes of an image the user uploaded: here the first ones of a PNG file
        byte[] statement = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        // begin-snippet: run-message
        var question = new ChatMessage(
            ChatRole.User,
            [
                new TextContent("Is this the charge on my statement?"),
                new DataContent(statement, "image/png")
            ]);

        var charge = await support.RunAsync(question, ct);
        // end-snippet

        Expect(charge.Text == answer.Text, "a run on a message with an image answers");

        await using (var request = provider.CreateAsyncScope())
        {
            var desk = request.ServiceProvider.GetRequiredService<RefundDesk>();
            var injected = await desk.AnswerAsync("Where is my refund?", CancellationToken.None);
            Expect(injected == answer.Text, "a host injected into a service answers");
        }

        var generic = await AgentAnswers.AskAsync(support, "Where is my refund?", CancellationToken.None);
        Expect(generic == $"{support.Agent.Name}: {answer.Text}", "a method generic over the agent runs it");

        var digest = new TicketDigest(provider.GetRequiredService<ITypeshiftRuntime>(), TimeProvider.System);
        var digested = await digest.DigestAsync(CancellationToken.None);
        Expect(digested.Length > 0, "a background job answers from a scope of its own");

        var tenants = new TenantDigest(provider.GetRequiredService<ITypeshiftRuntime>());
        var forTenant = await tenants.DigestAsync("acme", CancellationToken.None);
        Expect(forTenant.Length > 0, "a job that sets its tenant in its host's scope answers");

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

        var conversationId = 15;

        // begin-snippet: typed-run-try
        var chat = support.WithSettingsId(settingsId).WithConversationId(conversationId);

        var closing = await chat.RunAsync<RefundStatus>("Close ticket 1234.");

        if (closing.TryGetResult(out var status))
            Console.WriteLine($"{status.Invoice}: {status.State}");
        else
            Console.WriteLine($"No result yet: {closing.Decisions.Count} decision waits for an answer.");
        // end-snippet

        Expect(closing.Decisions.Count == 1, "a typed run that asks to list its decision");
        Expect(!closing.TryGetResult(out _), "a typed run that asks to have no result");

        var unread = await ThrowsAsync<InvalidOperationException>(() => Task.FromResult(closing.Result));
        Expect(unread, "the result of a typed run that asks to throw");

        await chat.CancelDecisionsAsync(ct);

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
        using var client = await support.WithSettingsId(settingsId).CreateChatClientAsync(settings: run);

        var response = await client.GetResponseAsync("Where is my refund?");
        // end-snippet

        Expect(response.Text == answer.Text, "the raw path answers like a run");

        await FromOptionsAsync(support, settingsId, run, answer.Text);
    }

    // The raw path's second form, for code that reads the options it hands the client
    private static async Task FromOptionsAsync(
        AgentHost<SupportAgent> support,
        int settingsId,
        SupportAgent.Settings run,
        string answer)
    {
        // begin-snippet: raw-path-options
        var options = await support.WithSettingsId(settingsId).ResolveOptionsAsync(settings: run);
        using var client = await support.CreateChatClientAsync(options);
        // end-snippet

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Where is my refund?")], options);

        Expect(options.Tools?.Count > 0, "resolved options carry the agent's tools");
        Expect(options.Instructions?.StartsWith("Answer Ada ") is true, "instructions built from the run's services");
        Expect(response.Text == answer, "a client from resolved options answers like a run");
    }

    private static void Expect(bool condition, string what)
    {
        if (!condition) throw new InvalidOperationException($"Expected {what}.");
    }

    private static async Task<bool> ThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
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
