using Agentic.Agents;
using Agentic.Tests.Toolsets.Companies;
using Agentic.Tests.Toolsets.Support;
using Microsoft.Extensions.AI;

namespace Agentic.Tests.Agents.Support;

public partial class ProbeAgent : AgentDefinition
{
    public ProbeAgent()
    {
        WithUniqueName("probe_agent");
        WithDescription("Probes the options and settings it runs with.");

        var options = new ChatOptions
        {
            Temperature = 0.2f,
            AllowMultipleToolCalls = true
        };

        WithChatOptions(options, picker => picker
            .Configurable(o => o.Temperature)
            .Configurable(o => o.MaxOutputTokens)
            .Configurable(o => o.Instructions));

        UseToolset<CompanyToolset>(CompanyToolset.FindCompanies, CompanyToolset.CreateCompanyProfile);
        UseToolset<ProbeToolset>();

        WithToolSettings(new ProbeSettings { Limit = 5, Mode = "thorough" });
    }
}
