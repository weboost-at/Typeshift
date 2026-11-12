using Microsoft.Extensions.AI;
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

        foreach (var request in answer.ApprovalRequests)
            if (request.ToolCall is FunctionCallContent call)
                Console.WriteLine($"Approve {call.Name}({string.Join(", ", call.Arguments!.Values)})?");
        // end-snippet

        // begin-snippet: approval-resume
        var approvals = answer.ApprovalRequests.Select(request => request.CreateResponse(approved: true));

        var approval = new ChatMessage(ChatRole.User, [.. approvals]);

        var resumed = await chat.RunAsync(approval);
        // end-snippet

        var ticket = await scope.ServiceProvider.GetRequiredService<ITicketClient>().GetAsync(1234, default);
        if (ticket is not { Closed: true }) throw new InvalidOperationException("Expected the approved call to run.");

        // begin-snippet: approval-closed
        try
        {
            await chat.RunAsync(approval);
        }
        catch (ApprovalClosedException closed)
        {
            foreach (var (request, reason) in closed.Requests)
                Console.WriteLine($"The approval {request} has expired: {reason}.");
        }
        // end-snippet

        // begin-snippet: approval-for-conversation
        var again = await chat.RunAsync("Close ticket 1234 again.");

        var always = new ChatMessage(ChatRole.User, [again.ApprovalRequests[0].ApproveForConversation()]);

        await chat.RunAsync(always);
        // end-snippet

        var later = await chat.RunAsync("Close ticket 1234 once more.");
        if (later.ApprovalRequests.Count > 0) throw new InvalidOperationException("Expected the standing approval.");
    }
}
