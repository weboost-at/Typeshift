using OpenAI.Chat;
using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Hosting;

// begin-snippet: agent-request-options
internal sealed class UnstoredBilling : IRunMiddleware
{
    public Task InvokeAsync(AgentRun run, Func<Task> next, CancellationToken cancellationToken)
    {
        if (run.Agent is BillingAgent)
            run.Options.RawRepresentationFactory = _ => new ChatCompletionOptions { StoredOutputEnabled = false };

        return next();
    }
}
// end-snippet
