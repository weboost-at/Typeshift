namespace Typeshift.Samples.Hosting;

// begin-snippet: context-provider
internal sealed class CustomerProfileContext(ICurrentCustomer customer) : IContextProvider
{
    public ValueTask<AgentContext> GetContextAsync(AgentRun run, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new AgentContext
        {
            Instructions = $"The customer is {customer.Name}, on the {customer.Plan} plan."
        });
}
// end-snippet
