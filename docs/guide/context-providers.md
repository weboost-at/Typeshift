---
title: Context providers
description: Add instructions, messages or tools to every run of an agent, such as memory, retrieved documents or the customer's profile.
---

# Context providers

A context provider adds context to every run of the agents that use it, such as memory, retrieved documents or the
customer's profile. It adds instructions, messages and tools to the run, and the conversation stores none of them. So
every run asks the provider again.

## Writing a provider

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

`GetContextAsync` returns an `AgentContext`. Each of its parts is appended to what the run already has:

| Part | Goes |
|---|---|
| `Instructions` | After the agent's instructions |
| `Messages` | Ahead of the input, after the resumed conversation |
| `Tools` | To this run only |

Typeshift calls the provider after the conversation is resumed, before the model is called.

## Using a provider

The agent names its providers in its constructor:

<!-- snippet: use-context -->
<a id='snippet-use-context'></a>
```cs
UseContext<CustomerProfileContext>();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L34-L36' title='Snippet source file'>snippet source</a> | <a href='#snippet-use-context' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Typeshift resolves the provider from the run's services, or creates it from them when none is registered. So a provider
needs no registration, and its constructor can take scoped services, such as the current customer.

## Context tools

Context tools are offered like the agent's own. One wrapped in `ApprovalRequiredAIFunction` asks for
[approval](./tool-approval.md).

::: warning Plan mode hides context tools
Context tools carry no `ReadOnly()` mark, so [plan mode](./modes.md#plan-mode) hides them.
:::

## Related

- [Agents](./defining-agents.md): `UseContext<T>()` among the other calls of an agent's constructor.
- [How a run works](./running-agents.md#how-a-run-works): when context is added in a run.
- [Middleware](./middleware.md): changing a run's messages and options, or answering in place of the model.
- [Tool approval](./tool-approval.md): what happens when a context tool asks.
