using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Scenarios;

internal static class Modes
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var triage = scope.ServiceProvider.GetRequiredService<AgentHost<TriageAgent>>();
        var settingsId = 42;
        var conversationId = 13;

        // begin-snippet: switch-mode
        var plan = new TriageAgent.Settings { Mode = AgentMode.Plan };

        var planned = await triage.RunAsync("Sort out ticket 1234.", settingsId, conversationId, plan);
        // end-snippet

        // begin-snippet: plan-mode
        var proposal = planned.ApprovalRequests.Single();

        if (proposal.ToolCall is FunctionCallContent { Arguments: { } arguments })
            Console.WriteLine(arguments["plan"]);
        // end-snippet

        // begin-snippet: approve-plan
        var approval = new ChatMessage(ChatRole.User, [proposal.ApprovePlan(AgentMode.Auto)]);

        await triage.RunAsync(approval, settingsId, conversationId);
        // end-snippet

        var store = provider.GetRequiredService<ITypeshiftStore>();
        var entries = await store.LoadConversationAsync(conversationId, default);

        if (entries[^1].Mode != AgentMode.Auto)
            throw new InvalidOperationException("Expected the run to end in auto mode.");
    }
}
