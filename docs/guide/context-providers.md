# Context providers

A context provider adds to every run of the agents that use it: memory, retrieved documents, the customer's profile.

<!-- snippet: context-provider -->
<a id='snippet-context-provider'></a>
```cs
internal sealed class CustomerProfileContext(ICurrentCustomer customer) : IContextProvider
{
    public ValueTask<AgentContext> GetContextAsync(AgentRun run, CancellationToken ct) =>
        ValueTask.FromResult(new AgentContext
        {
            Instructions = $"The customer is {customer.Name}, on the {customer.Plan} plan."
        });
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/CustomerProfileContext.cs#L3-L12' title='Snippet source file'>snippet source</a> | <a href='#snippet-context-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The agent names its providers in its constructor:

<!-- snippet: use-context -->
<a id='snippet-use-context'></a>
```cs
UseContext<CustomerProfileContext>();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L28-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-use-context' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A provider is resolved from the run's services, or created from them when none is registered, and called after the
conversation is resumed, before the model is called. What it returns is appended: instructions after the agent's,
messages ahead of the input, tools to this run only. Its messages are never stored with the conversation, so every
run asks again.

Context tools are offered like the agent's own: one wrapped in `ApprovalRequiredAIFunction` asks for
[approval](./tool-approval.md), and plan mode hides them, as they carry no `ReadOnly()` mark.
