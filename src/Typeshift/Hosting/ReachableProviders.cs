namespace Typeshift;

// D87: the providers of the agents a run may call as sub-agents or hand off to, each as its own run would resolve it
// without the call site: from its stored settings under the run's settings id where they may name one, else its code,
// else the default provider. A run checks they are served before it stores anything, so a sub-agent nobody serves
// fails the run at its start, and not each of its calls while the run goes on. D89: so are the providers their own
// model policies fall back to, which a policy the call site handed the run replaces
internal static class ReachableProviders
{
    public static async Task EnsureServedAsync(
        Agent agent,
        IServiceProvider services,
        StoreAccess store,
        StoreId? settingsId,
        ProviderId? handedProvider,
        ModelPolicy? handedPolicy,
        CancellationToken ct)
    {
        foreach (var reached in Reach(agent, services))
        {
            var model = reached.ChatOptions.ModelId;

            if (handedProvider is null)
            {
                var options = await ResolveAsync(reached, services, store, settingsId, ct).ConfigureAwait(false);
                ChatClients.EnsureServed(services, $"the agent '{reached.Name}'", options.Provider, options.ModelId);
                model = options.ModelId;
            }

            if (handedPolicy is not null) continue;

            foreach (var rule in reached.RunOptions.ModelPolicy.Rules.Where(rule => rule.Provider is not null))
            {
                var policy = $"the model policy of the agent '{reached.Name}'";
                ChatClients.EnsureServed(services, policy, rule.Provider, rule.Model ?? model);
            }
        }
    }

    // The agent's sub-agents and handoffs, theirs in turn, each once and without the agent itself
    private static List<Agent> Reach(Agent agent, IServiceProvider services)
    {
        var reached = new List<Agent>();
        var next = new Queue<Agent>([agent]);

        while (next.TryDequeue(out var from))
        {
            var hosts = from.SubAgents
                .Select(use => use.Host(services))
                .Concat(from.Handoffs.Select(handoff => handoff(services)));

            foreach (var host in hosts.Where(host => host.Agent != agent && !reached.Contains(host.Agent)))
            {
                reached.Add(host.Agent);
                next.Enqueue(host.Agent);
            }
        }

        return reached;
    }

    // Only settings that store a provider are loaded, so a run whose agents name theirs in code reads nothing more
    private static async Task<ResolvedChatOptions> ResolveAsync(
        Agent agent,
        IServiceProvider services,
        StoreAccess store,
        StoreId? settingsId,
        CancellationToken ct)
    {
        var declaration = agent.GeneratedDeclaration;
        var options = new ResolvedChatOptions(agent.ChatOptions) { Provider = agent.Provider };

        if (declaration.StoresProvider)
        {
            var layers = await declaration.LoadLayersAsync(store, settingsId, ct).ConfigureAwait(false);

            foreach (var layer in layers.OfType<object>())
            {
                declaration.ApplySettings(options, agent.RunOptions, layer);
            }
        }

        options.Provider ??= ChatClients.DefaultProvider(services);
        return options;
    }
}
