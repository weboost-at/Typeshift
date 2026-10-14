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

        Console.WriteLine(answer.Text);
        // end-snippet

        // begin-snippet: run-with-id
        var customer = 42;

        var stored = await support.RunAsync("Where is my refund?", id: customer);
        // end-snippet

        // begin-snippet: run-settings
        var run = new SupportAgent.Settings
        {
            Model = "reasoning-model",
            Temperature = 0,
            SearchTickets = new TicketToolset.SearchTicketsOverrides { IncludeClosed = true },
            SearchArticles = new KnowledgeToolset.SearchArticlesOverrides { Language = "de" }
        };

        var thorough = await support.RunAsync("Where is my refund?", id: customer, settings: run);
        // end-snippet

        // begin-snippet: structured-output
        var report = await support.RunAsync<RefundStatus>("Summarize the refund.", id: customer);

        Console.WriteLine($"{report.Result.Invoice}: {report.Result.State}");
        // end-snippet

        // begin-snippet: streaming
        await foreach (var update in support.RunStreamingAsync("Where is my refund?", id: customer))
            Console.Write(update.Text);
        // end-snippet

        // begin-snippet: raw-path
        var options = await support.ResolveOptionsAsync(id: customer, settings: run);
        options.ConversationId = "7";

        var client = await support.CreateChatClientAsync(options);
        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Where is my refund?")], options);
        // end-snippet
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
