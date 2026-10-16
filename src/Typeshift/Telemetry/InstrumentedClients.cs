using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Typeshift;

// D36: one instrumented client per provider client, kept as long as it. A source and a meter per run would reset the
// metrics after every run, since the OpenTelemetry SDK ends the instruments of a disposed meter
internal sealed class InstrumentedClients : IDisposable
{
    private readonly ConditionalWeakTable<IChatClient, OpenTelemetryChatClient> _instrumented = new();

    // A run's pipeline disposes what it wraps, and neither the instrumented client nor the provider's is its to dispose
    public IChatClient Instrument(IChatClient client) =>
        new UndisposedChatClient(_instrumented.GetValue(
            client,
            static provided => new OpenTelemetryChatClient(
                new UndisposedChatClient(provided),
                sourceName: Telemetry.SourceName)));

    public void Dispose()
    {
        foreach (var (_, instrumented) in _instrumented)
        {
            instrumented.Dispose();
        }
    }
}
