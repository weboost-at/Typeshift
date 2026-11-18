namespace Typeshift.Tests.Testing;

// The bytes a piece of work allocates on the thread that runs it: the same on a loaded machine as on an idle one,
// where a time is not
public static class Allocations
{
    // Tests that measure run beside no other test, whose work on the same thread would count as theirs
    public const string Collection = "Allocations";

    public static async Task<long> OfAsync(Func<Task> work)
    {
        var thread = Environment.CurrentManagedThreadId;
        var before = GC.GetAllocatedBytesForCurrentThread();

        await work();

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        return Environment.CurrentManagedThreadId == thread
            ? allocated
            : throw new InvalidOperationException("The work left its thread, so its allocations were not counted.");
    }
}

[CollectionDefinition(Allocations.Collection, DisableParallelization = true)]
public sealed class AllocationsCollection;
