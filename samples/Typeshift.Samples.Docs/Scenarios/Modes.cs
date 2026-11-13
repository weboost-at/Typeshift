using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Scenarios;

internal static class Modes
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var triage = scope.ServiceProvider.GetRequiredService<AgentHost<TriageAgent>>();
        var conversationId = 13;

        // begin-snippet: switch-mode
        var plan = new TriageAgent.Settings { Mode = AgentMode.Plan };
        var chat = triage.WithConversationId(conversationId);

        var planned = await chat.RunAsync("Sort out ticket 1234.", settings: plan);
        // end-snippet

        // begin-snippet: plan-mode
        var proposal = planned.Decisions.OfType<PlanDecision>().Single();

        Console.WriteLine(proposal.Plan);
        // end-snippet

        // begin-snippet: approve-plan
        await chat.AnswerAsync([proposal.Approve(AgentMode.Auto)]);
        // end-snippet

        var store = provider.GetRequiredService<ITypeshiftStore<int>>();
        var entries = await store.LoadConversationAsync(conversationId, default);

        if (entries[^1].Mode != AgentMode.Auto)
            throw new InvalidOperationException("Expected the run to end in auto mode.");
    }
}
