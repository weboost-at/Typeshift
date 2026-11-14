using System.Collections.Concurrent;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.MultiAgent.Support;

// The names of the agents whose runs ended, in order, a background sub-agent's included
public sealed class RunEnds : ConcurrentQueue<string>;

// The steering input run middleware was shown, in order
public sealed class SteeringSeen : ConcurrentQueue<string>;

// Records a run's end once everything inside it is done, after a hop to the thread pool, as a client's cleanup may
// take one, and the steering input it is shown
public sealed class RunEndMiddleware(RunEnds ends, SteeringSeen steering) : IRunMiddleware
{
    public ValueTask<ChatMessage?> OnSteeringAsync(AgentRun run, ChatMessage input, CancellationToken ct)
    {
        steering.Enqueue(input.Text);
        return new(input);
    }

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
