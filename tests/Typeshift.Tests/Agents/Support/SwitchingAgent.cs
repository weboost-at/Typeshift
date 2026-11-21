using Typeshift.Tests.Tools.Support;
using Typeshift.Tests.Toolsets;

namespace Typeshift.Tests.Agents.Support;

// Lets settings switch the probe off, beside a tool they cannot switch off
public partial class SwitchingAgent : Agent
{
    public SwitchingAgent()
    {
        WithChatOptions()
            .WithProvider(ProbeAgent.ModelProvider)
            .WithModel(ProbeAgent.Model);

        UseToolset<ProbeToolset>()
            .Configurable(t => t.Probe);

        UseToolset<CompanyToolset>()
            .Include(t => t.CountCompanies);
    }
}
