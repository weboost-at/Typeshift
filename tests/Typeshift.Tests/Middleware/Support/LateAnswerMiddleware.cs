using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Middleware.Support;

// The one run middleware of a host, with no other inside it: it lets the model answer, then answers again
public sealed class LateAnswerMiddleware : IRunMiddleware
{
    public const string Answer = "An answer after the model's.";

    public async Task InvokeAsync(AgentRun run, Func<Task> next, CancellationToken ct)
    {
        await next();
        run.Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, Answer));
    }
}
