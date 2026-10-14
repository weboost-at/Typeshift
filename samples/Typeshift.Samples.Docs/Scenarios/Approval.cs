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
        var conversationId = 8;

        // begin-snippet: approval-run
        var answer = await support.RunAsync("Close ticket 1234.", conversationId: conversationId);

        foreach (var request in answer.ApprovalRequests)
            if (request.ToolCall is FunctionCallContent call)
                Console.WriteLine($"Approve {call.Name}({string.Join(", ", call.Arguments!.Values)})?");
        // end-snippet

        // begin-snippet: approval-resume
        var approvals = answer.ApprovalRequests.Select(request => request.CreateResponse(approved: true));

        var approval = new ChatMessage(ChatRole.User, [.. approvals]);

        var resumed = await support.RunAsync(approval, conversationId: conversationId);
        // end-snippet

        var ticket = await scope.ServiceProvider.GetRequiredService<ITicketClient>().GetAsync(1234, default);
        if (ticket is not { Closed: true }) throw new InvalidOperationException("Expected the approved call to run.");

        var again = await support.RunAsync("Close ticket 1234 again.", conversationId: conversationId);

        // begin-snippet: approval-for-conversation
        var always = new ChatMessage(ChatRole.User, [again.ApprovalRequests[0].ApproveForConversation()]);

        await support.RunAsync(always, conversationId: conversationId);
        // end-snippet

        var later = await support.RunAsync("Close ticket 1234 once more.", conversationId: conversationId);
        if (later.ApprovalRequests.Count > 0) throw new InvalidOperationException("Expected the standing approval.");
    }
}
