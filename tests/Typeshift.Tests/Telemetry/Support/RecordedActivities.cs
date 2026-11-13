using System.Collections.Concurrent;
using System.Diagnostics;

namespace Typeshift.Tests.Telemetry.Support;

// Every span of the Typeshift source from its creation on, as an exporter would receive them
public sealed class RecordedActivities
{
    private readonly ConcurrentQueue<Activity> _stopped = new();

    public RecordedActivities()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Typeshift",
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _stopped.Enqueue
        };

        ActivitySource.AddActivityListener(listener);
    }

    // The run of a conversation, tests running alongside have their own
    public Activity Run(Guid conversation) => Run(conversation.ToString());

    // A conversation id as the span carries it, read or not
    public Activity Run(string conversation) => Runs(conversation).Single();

    public IReadOnlyList<Activity> Runs(Guid conversation) => Runs(conversation.ToString());

    public IReadOnlyList<Activity> Runs(string conversation) =>
    [
        .. _stopped.Where(activity =>
            activity.GetTagItem("gen_ai.operation.name") is "invoke_agent"
            && activity.GetTagItem("gen_ai.conversation.id") as string == conversation)
    ];

    public IReadOnlyList<Activity> Inside(Activity run) =>
        [.. _stopped.Where(activity => activity.TraceId == run.TraceId && activity != run)];

    // The run's children; under a request's span every span is in the request's trace, wherever it hangs
    public IReadOnlyList<Activity> Under(Activity run) =>
        [.. _stopped.Where(activity => activity.ParentSpanId == run.SpanId)];
}
