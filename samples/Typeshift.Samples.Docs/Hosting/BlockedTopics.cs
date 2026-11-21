using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Hosting;

// begin-snippet: run-middleware
internal sealed class BlockedTopics : IRunMiddleware
{
    public Task InvokeAsync(AgentRun run, Func<Task> next, CancellationToken cancellationToken)
    {
        if (IsBlocked(run.Messages[^1]))
        {
            run.Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "I can't help with passwords."));
            return Task.CompletedTask;
        }

        return next();
    }

    public ValueTask<ChatMessage?> OnSteeringAsync(
        AgentRun run,
        ChatMessage input,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(IsBlocked(input) ? null : input);

    private static bool IsBlocked(ChatMessage message) =>
        message.Text.Contains("password", StringComparison.OrdinalIgnoreCase);
}
// end-snippet
