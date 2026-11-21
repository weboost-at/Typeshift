using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Typeshift.Samples.Quickstart;

namespace Typeshift.Samples.Scenarios;

// The spans of a run as an exporter receives them: the run's chat and tool spans hang under its invoke_agent span,
// whether the run streams or not. Before that, the export to a local dashboard, built and resolved; no span is made
// while it listens, so it sends nothing
internal static class Observability
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        var services = new ServiceCollection();
        ShowOtlpExport(services);

        await using (var exporting = services.BuildServiceProvider())
        {
            exporting.GetRequiredService<TracerProvider>();
            if (!new ActivitySource("Typeshift").HasListeners())
                throw new InvalidOperationException("Expected the OTLP export to listen to Typeshift's spans.");
        }

        var spans = new ConcurrentQueue<Activity>();

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Typeshift",
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = spans.Enqueue
        };

        ActivitySource.AddActivityListener(listener);

        await using var scope = provider.CreateAsyncScope();
        var research = scope.ServiceProvider.GetRequiredService<AgentHost<ResearchAgent>>();

        await research.RunAsync("Vienna");
        ExpectOneTree(spans, "a run");

        spans.Clear();

        await foreach (var _ in research.RunStreamingAsync("Graz"))
        {
        }

        ExpectOneTree(spans, "a streaming run");
    }

    public static void ShowOtlpExport(IServiceCollection services)
    {
        // begin-snippet: otlp-export
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource("Typeshift"))
            .WithMetrics(metrics => metrics.AddMeter("Typeshift"))
            .WithLogging()
            .UseOtlpExporter();
        // end-snippet
    }

    private static void ExpectOneTree(IReadOnlyCollection<Activity> spans, string run)
    {
        if (spans.Where(span => span.DisplayName == "invoke_agent research_agent").ToList() is not [var agent])
            throw new InvalidOperationException($"Expected one invoke_agent span for {run}.");

        var others = spans.Where(span => span != agent).ToList();

        if (!others.Any(span => span.DisplayName.StartsWith("chat "))
            || !others.Any(span => span.DisplayName == "execute_tool find_companies"))
            throw new InvalidOperationException($"Expected the chat and tool spans of {run}.");

        if (!others.All(span => Ancestors(span).Contains(agent)))
            throw new InvalidOperationException($"Expected every span of {run} under its invoke_agent span.");
    }

    private static IEnumerable<Activity> Ancestors(Activity span)
    {
        for (var parent = span.Parent; parent is not null; parent = parent.Parent)
            yield return parent;
    }
}
