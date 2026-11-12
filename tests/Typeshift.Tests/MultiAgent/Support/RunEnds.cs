using System.Collections.Concurrent;

namespace Typeshift.Tests.MultiAgent.Support;

// The names of the agents whose runs ended, in order, a background sub-agent's included
public sealed class RunEnds : ConcurrentQueue<string>;

// Records a run's end once everything inside it is done, after a hop to the thread pool, as a client's cleanup may
// take one
public sealed class RunEndMiddleware(RunEnds ends) : IRunMiddleware
{
    public async Task InvokeAsync(AgentRun run, Func<Task> next, CancellationToken ct)
    {
        try
        {
            await next();
        }
        finally
        {
            await Task.Yield();
            ends.Enqueue(run.Agent.Name);
        }
    }
}
