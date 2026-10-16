using System.Diagnostics;

namespace Typeshift.Demos.Hosting;

/// <summary>
/// Prints every span of the Typeshift source as it ends, so a demo shows what an OpenTelemetry exporter would get.
/// </summary>
public sealed class ConsoleSpans : IDisposable
{
    private readonly ActivityListener _listener;

    public ConsoleSpans()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Typeshift",
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = Print
        };

        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    private static void Print(Activity activity)
    {
        var depth = 0;

        for (var parent = activity.Parent; parent is not null; parent = parent.Parent)
        {
            depth++;
        }

        var tokens = activity.GetTagItem("gen_ai.usage.input_tokens") is { } input
            ? $" tokens in={input} out={activity.GetTagItem("gen_ai.usage.output_tokens")}"
            : "";

        var error = activity.Status == ActivityStatusCode.Error ? $" ERROR {activity.StatusDescription}" : "";

        Console.Error.WriteLine(
            $"  [span] {new string(' ', depth * 2)}{activity.DisplayName} {activity.Duration.TotalMilliseconds:F0}ms{tokens}{error}");
    }
}
