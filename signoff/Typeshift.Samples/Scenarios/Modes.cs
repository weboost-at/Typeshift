using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Scenarios;

internal static class Modes
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var coding = scope.ServiceProvider.GetRequiredService<AgentHost<CodingAgent>>();
        var settingsId = 42;
        var conversationId = 13;

        // begin-snippet: plan-mode
        var planned = await coding.RunAsync("Move the solution to .NET 10.", settingsId, conversationId);

        var proposal = planned.ApprovalRequests.Single();
        // end-snippet

        // begin-snippet: approve-plan
        var approval = new ChatMessage(ChatRole.User, [proposal.ApprovePlan(AgentMode.Auto)]);

        await coding.RunAsync(approval, settingsId, conversationId);
        // end-snippet

        // begin-snippet: switch-mode
        var plan = new CodingAgent.Settings { Mode = AgentMode.Plan };

        await coding.RunAsync("Now plan the move to central package management.", settingsId, conversationId, plan);
        // end-snippet
    }
}
