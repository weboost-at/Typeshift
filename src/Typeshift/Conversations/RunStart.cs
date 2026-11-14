using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D34, D50: how a run takes its conversation, before anything of its agent resolves. It claims the conversation and
// loads it from its last summary, once: for the agent that answers, which takes both on, and for the run's history.
// It saves the mode the run's settings name, so that agent runs in it too (D52). And it learns which agent answers:
// the one that answered the conversation last, if this agent's handoffs reach it
internal sealed class RunStart(Agent agent, IServiceProvider services, DecisionLedger ledger)
{
    // What the run goes on with, the claim and the entries among it, and the agent that answers in this one's place
    // if another does. A run without a conversation takes nothing, and one handed the entries was passed on whole
    public async Task<(RunHandover Taken, IAgentHost? Answering)> TakeAsync(
        RunHandover handover,
        AgentMode? named,
        CancellationToken ct)
    {
        if (handover is not { Conversation: { } conversation, Entries: null }) return (handover, null);

        var runs = services.GetRequiredService<LiveRuns>();

        // A run handed a claim takes it in place of claiming
        var claim = handover.Claim ?? await runs.ClaimAsync(conversation, ct);

        try
        {
            var entries = await services.GetRequiredService<StoreAccess>()
                .LoadConversationSinceCompactionAsync(conversation, ct);

            // D78: said once per load, since the run goes on without what the entries left out
            if (entries.Sum(entry => entry.UnreadContents) is > 0 and var unread)
            {
                LogEvents.ContentNotRead(LogEvents.For(services), conversation, unread);
            }

            // D52: a run whose settings name a mode switches the conversation to it before anyone answers, so the
            // agent the run is passed on or handed to runs in it too
            if (named is { } mode) await ledger.SetModeAsync(conversation, mode, ct);

            return (handover with { Claim = claim, Entries = entries }, AnsweringAgent(entries));
        }
        catch
        {
            runs.Release(claim);
            throw;
        }
    }

    // D34: the agent that answered the conversation last answers its next run, if this agent's handoffs reach it
    private IAgentHost? AnsweringAgent(IReadOnlyList<ConversationEntry> entries) =>
        agent.Handoffs.Count > 0 && entries.Count > 0 && entries[^1].Agent != agent.Name
            ? Reachable().GetValueOrDefault(entries[^1].Agent)
            : null;

    private Dictionary<string, IAgentHost> Reachable()
    {
        var reached = new Dictionary<string, IAgentHost>();
        var next = new Queue<Agent>([agent]);

        while (next.TryDequeue(out var from))
        {
            foreach (var host in from.Handoffs.Select(handoff => handoff(services)))
            {
                if (host.Agent.Name != agent.Name && reached.TryAdd(host.Agent.Name, host)) next.Enqueue(host.Agent);
            }
        }

        return reached;
    }
}
