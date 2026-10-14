using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Tools.Support;

public record ProbeSettings
{
    public int Limit { get; init; } = 10;

    public bool Verbose { get; init; }
}

// Scoped, so two calls that see the same id ran in the same scope
public sealed class CallScope
{
    public Guid Id { get; } = Guid.NewGuid();
}

public record ProbeResult(
    string Query,
    ProbeSettings Settings,
    Guid? Scope,
    bool ArgumentsCarryServices,
    bool Cancellable);

// Returns what a call received, so a test can read it back
public partial class ProbeToolset : Toolset
{
    public ProbeToolset()
    {
        AddTool(Probe)
            .ReadOnly()
            .WithSettings<ProbeSettings>()
            .Configurable(s => s.Limit);
    }

    /// <summary>
    /// Returns what the call received.
    /// </summary>
    /// <param name="query">What to probe.</param>
    public static ProbeResult Probe(
        string query,
        ProbeSettings settings,
        IServiceProvider services,
        AIFunctionArguments arguments,
        CancellationToken ct) =>
        new(
            Query: query,
            Settings: settings,
            Scope: services.GetService<CallScope>()?.Id,
            ArgumentsCarryServices: ReferenceEquals(arguments.Services, services),
            Cancellable: ct.CanBeCanceled);
}
