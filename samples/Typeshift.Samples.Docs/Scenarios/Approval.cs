using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Tickets;

namespace Typeshift.Samples.Scenarios;

internal static class Approval
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();

        // begin-snippet: approval-run
        var chat = support.WithConversationId(8);
        var answer = await chat.RunAsync("Close ticket 1234.");

        var approvals = answer.Decisions.OfType<ApprovalDecision>().ToList();

        foreach (var approval in approvals)
            Console.WriteLine($"Approve {approval.Tool}({approval.Arguments})?");
        // end-snippet

        // begin-snippet: approval-resume
        var resumed = await chat.AnswerAsync([.. approvals.Select(approval => approval.Approve())]);
        // end-snippet

        var ticket = await scope.ServiceProvider.GetRequiredService<ITicketClient>().GetAsync(1234, default);
        if (ticket is not { Closed: true }) throw new InvalidOperationException("Expected the approved call to run.");
        if (resumed.Decisions.Count > 0) throw new InvalidOperationException("Expected nothing left to decide.");

        // begin-snippet: approval-closed
        try
        {
            await chat.AnswerAsync([approvals[0].Approve()]);
        }
        catch (DecisionClosedException closed)
        {
            foreach (var (decision, reason) in closed.Decisions)
                Console.WriteLine($"The decision {decision} is closed: {reason}.");
        }
        // end-snippet

        // begin-snippet: approval-for-conversation
        var again = await chat.RunAsync("Close ticket 1234 again.");
        var asked = again.Decisions.OfType<ApprovalDecision>().Single();

        await chat.AnswerAsync([asked.ApproveForConversation()]);
        // end-snippet

        var later = await chat.RunAsync("Close ticket 1234 once more.");
        if (later.Decisions.Count > 0) throw new InvalidOperationException("Expected the standing approval.");

        // begin-snippet: approval-standing
        var decisions = await chat.GetDecisionsAsync();

        foreach (var standing in decisions.Standing)
            Console.WriteLine($"{standing.Tool} runs without asking since {standing.GrantedAt:t}.");
        // end-snippet

        if (decisions.Standing is not [{ Tool: "close_ticket" }])
            throw new InvalidOperationException("Expected the standing approval to be listed.");
    }
}
