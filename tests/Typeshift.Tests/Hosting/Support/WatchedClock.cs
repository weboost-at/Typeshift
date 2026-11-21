using Microsoft.Extensions.Time.Testing;

namespace Typeshift.Tests.Hosting.Support;

// The test's clock, which tells the test once a run started a timer of the time it watches, so the test moves the
// clock on only after the run waits
public sealed class WatchedClock(FakeTimeProvider time, TimeSpan watched) : TimeProvider
{
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => _started.Task;

    public override long TimestampFrequency => time.TimestampFrequency;

    public override TimeZoneInfo LocalTimeZone => time.LocalTimeZone;

    public override DateTimeOffset GetUtcNow() => time.GetUtcNow();

    public override long GetTimestamp() => time.GetTimestamp();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = time.CreateTimer(callback, state, dueTime, period);
        if (dueTime == watched) _started.TrySetResult();

        return timer;
    }
}
