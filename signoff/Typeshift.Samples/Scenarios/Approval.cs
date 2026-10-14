using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Scenarios;

internal static class Approval
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();
        var conversationId = 7;

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
    }
}
