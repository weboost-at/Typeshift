namespace Typeshift.Tests.Persistence.Support;

// A clock that refuses every wait, so a test sees whose clock a wait asks
public sealed class RefusingClock : TimeProvider
{
    public const string Refusal = "The app's clock was asked to wait.";

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        throw new NotSupportedException(Refusal);
}
