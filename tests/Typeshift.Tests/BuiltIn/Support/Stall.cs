namespace Typeshift.Tests.BuiltIn.Support;

// A wait that ends only when its token is cancelled, as on a server that stopped answering
public static class Stall
{
    public static Task<T> Until<T>(CancellationToken ct)
    {
        var stalled = new TaskCompletionSource<T>();
        ct.Register(() => stalled.TrySetCanceled(ct));
        return stalled.Task;
    }
}
