using Typeshift.Tests.Tools.Support;

namespace Typeshift.Tests.Agents.Support;

// Offers a toolset's tool and a tool of its own, declared and configured like the toolset's
public partial class OwnToolsAgent : Agent
{
    public OwnToolsAgent()
    {
        WithChatOptions()
            .WithModel(ProbeAgent.ModelProvider, ProbeAgent.Model);

        UseToolset<ProbeToolset>();

        AddTool(Recall)
            .ReadOnly()
            .WithSettings<ProbeSettings>()
            .Configurable(s => s.Limit);

        ConfigureRecall()
            .WithVerbose(true);
    }

    /// <summary>
    /// Returns what the call received.
    /// </summary>
    /// <param name="query">What to recall.</param>
    private static ProbeResult Recall(string query, ProbeSettings settings) =>
        new(Query: query, Settings: settings, Scope: null, ArgumentsCarryServices: false, Cancellable: false);
}
