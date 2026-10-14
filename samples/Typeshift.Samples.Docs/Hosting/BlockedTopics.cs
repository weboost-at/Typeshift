using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Hosting;

// begin-snippet: run-middleware
internal sealed class BlockedTopics : IRunMiddleware
{
    public Task InvokeAsync(AgentRun run, Func<Task> next, CancellationToken ct)
    {
        if (run.Messages[^1].Text.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            run.Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "I can't help with passwords."));
            return Task.CompletedTask;
        }

        return next();
    }
}
// end-snippet
