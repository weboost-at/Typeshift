using Typeshift.Agents;
using Typeshift.Tests.Toolsets.Companies;
using Typeshift.Tests.Toolsets.Support;

namespace Typeshift.Tests.Agents.Support;

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
            .Include(t => t.FindCompanies)
            .Include(t => t.CreateCompanyProfile);

        UseToolset<ProbeToolset>();

        ConfigureProbe()
            .WithLimit(5)
            .WithMode("thorough");
    }

    protected override string GetInstructions() => CodeInstructions;
}
