using Agentic.Agents;
using Agentic.Tests.Toolsets.Companies;
using Agentic.Tests.Toolsets.Support;

namespace Agentic.Tests.Agents.Support;

public partial class ProbeAgent : AgentDefinition
{
    public const string CodeInstructions =
        """
        Probe the options and settings you run with.
        Report them as they are.
        """;

    public ProbeAgent()
    {
        WithName("probe_agent");
        WithDescription("Probes the options and settings it runs with.");

        WithChatOptions()
            .WithTemperature(0.2f)
            .WithAllowMultipleToolCalls(true)
            .Configurable(o => o.Temperature)
            .Configurable(o => o.MaxOutputTokens)
            .Configurable(o => o.Instructions);

        UseToolset<CompanyToolset>()
            .FindCompanies()
            .CreateCompanyProfile();

        UseToolset<ProbeToolset>();

        WithToolSettings()
            .Probe()
            .WithLimit(5)
            .WithMode("thorough");
    }

    protected override string GetInstructions() => CodeInstructions;
}
