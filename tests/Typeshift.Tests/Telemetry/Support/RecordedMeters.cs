using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Typeshift.Tests.Telemetry.Support;

// The instruments of the Typeshift meters that measured the calls to one backend, as an exporter would see them, and
// which of them ended, as an exporter learns when a meter is disposed; until it is disposed itself
public sealed class RecordedMeters : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly ConcurrentDictionary<Instrument, string> _measured = [];
    private readonly ConcurrentDictionary<Instrument, string> _ended = [];

    // Tests running alongside measure their own backends under meters of the same name, so the host tells them apart
    public RecordedMeters(string host)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Typeshift") listener.EnableMeasurementEvents(instrument);
        };

        _listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) => Record(instrument, tags, host));
        _listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => Record(instrument, tags, host));
        _listener.SetMeasurementEventCallback<int>((instrument, _, tags, _) => Record(instrument, tags, host));
        _listener.MeasurementsCompleted = (instrument, _) => _ended[instrument] = instrument.Name;
        _listener.Start();
    }

    // The names of the instruments that measured the backend's calls and whose meter ended since
    public IReadOnlyList<string> Ended =>
        [.. _measured.Keys.Where(_ended.ContainsKey).Select(instrument => instrument.Name).Distinct().Order()];

    public void Dispose() => _listener.Dispose();

    private void Record(Instrument instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags, string host)
    {
        foreach (var (name, value) in tags)
        {
            if (name == "server.address" && value as string == host) _measured[instrument] = instrument.Name;
        }
    }
}
