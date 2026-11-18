namespace Typeshift.Tests.Testing;

// The bytes a piece of work allocates on the thread that runs it: the same on a loaded machine as on an idle one,
// where a time is not
public static class Allocations
{
    // Tests that measure run beside no other test, whose work on the same thread would count as theirs
    public const string Collection = "Allocations";

    // Counted only when the work finished before it returned: work that waited ran in part on other threads, which
    // the count misses, while this thread was free to run other work, which it would count
    public static async Task<long> OfAsync(Func<Task> work)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var running = work();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        if (!running.IsCompleted)
            throw new InvalidOperationException("The work waited, so its allocations were not counted on one thread.");

        await running;
        return allocated;
    }
}

[CollectionDefinition(Allocations.Collection, DisableParallelization = true)]
public sealed class AllocationsCollection;
