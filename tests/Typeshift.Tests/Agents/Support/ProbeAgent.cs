using Typeshift.Tests.Tools.Support;
using Typeshift.Tests.Toolsets;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Agents.Support;

// Offers the probe, which returns the settings it ran with, so a test reads back what a run resolved
public partial class ProbeAgent : Agent
{
    public const string ModelProvider = "probe-provider";

    public const string Model = "probe-model";

    public const float Temperature = 0.5f;

    public const string Stop = "END";

    public const string Prompt = "Probe what the user asks.";

    // Outside the probe's code default and the limits the test data picks
    public const int ConfiguredLimit = 100;

    public const int MaxToolIterations = 5;

    public ProbeAgent()
    {
        WithDescription("Probes what a run resolves.");

        WithChatOptions()
            .WithModel(ModelProvider, Model)
            .WithTemperature(Temperature)
            .WithStopSequences(Stop)
            .WithTools(new HostedWebSearchTool())
            .Configurable(o => o.ModelId)
            .Configurable(o => o.Temperature)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.StopSequences);

        WithRunOptions()
            .WithMaxToolIterations(MaxToolIterations)
            .Configurable(r => r.MaxToolIterations);

        UseToolset<ProbeToolset>();

        UseToolset<CompanyToolset>().Include(t => t.CountCompanies).Include(t => t.FindCompanies);

        ConfigureProbe()
            .WithLimit(ConfiguredLimit)
            .WithVerbose(true);
    }

    protected override string GetInstructions() => Prompt;
}
