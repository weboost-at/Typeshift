using Agentic.Toolsets;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Tests.Toolsets.Support;

public record ProbeSettings
{
    public int Limit { get; init; } = 10;

    public string Mode { get; init; } = "fast";
}

public record ProbeResult(string Query, ProbeSettings Settings, Guid? Scope);

public partial class ProbeToolset : Toolset
{
    public ProbeToolset()
    {
        AddTool(Probe)
            .WithName("probe")
            .WithDescription("Returns what it was called with and the settings it runs with.")
            .WithSettings<ProbeSettings>()
            .Configurable(s => s.Limit);
    }

    public static ProbeResult Probe(
        string query,
        ProbeSettings settings,
        IServiceProvider serviceProvider,
        AIFunctionArguments arguments,
        CancellationToken ct) =>
        new(query, settings, serviceProvider.GetService<CallScope>()?.Id);
}
