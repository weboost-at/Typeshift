---
title: Middleware and context
description: Wrap every run or tool call for logging, guardrails or audits, and add memory, documents or the customer's profile to an agent's runs.
---

# Middleware and context

Middleware wraps every run or tool call, for logging, guardrails or limits. A context provider adds instructions,
messages or tools to the runs of the agents that use it, such as a memory.

## Middleware

### Around runs

<!-- snippet: run-middleware -->
<a id='snippet-run-middleware'></a>
```cs
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
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/BlockedTopics.cs#L5-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-middleware' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

An `IRunMiddleware` sees the run's agent, messages, options and mode. It may change the messages and the options
before it calls `next`, and once `next` returns, `run.Response` holds the answer. The changes hold for this run only,
because the conversation keeps the messages the host sent. Middleware runs for every agent, so check `run.Agent` when
it should act on some of them only.

### Answering in place of the model

Set `run.Response` and skip `next`, as `BlockedTopics` does above, and the middleware answers in place of the model.
The conversation stores that answer. Middleware that neither calls `next` nor sets `run.Response` fails the run.

### Streaming runs

The same `InvokeAsync` serves streaming runs. The updates stream on to the caller while `next` runs, and `next`
returns when the stream ends. A response set in place of the model is streamed as it is.

::: warning A steered run passes through middleware again
Steering input that arrives after a run's answer starts another pass of the run, and that pass goes through the
middleware again, so middleware can see one run more than once. See [Conversations](./conversations.md#steering).
:::

### Around tool calls

<!-- snippet: tool-middleware -->
<a id='snippet-tool-middleware'></a>
```cs
internal sealed class AuditToolCalls(ICurrentCustomer customer, ILogger<AuditToolCalls> logger) : IToolMiddleware
{
    public async ValueTask<object?> InvokeAsync(
        FunctionInvocationContext call,
        Func<ValueTask<object?>> next,
        CancellationToken ct)
    {
        logger.LogInformation("Customer {Customer} calls {Tool}", customer.Id, call.Function.Name);

        return await next();
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/AuditToolCalls.cs#L6-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-tool-middleware' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

An `IToolMiddleware` receives Microsoft.Extensions.AI's `FunctionInvocationContext`: the function, its arguments and
the messages so far. What it returns is the result the model receives, so returning without calling `next` answers
in place of the tool. Setting `call.Terminate` ends the tool loop.

### Registering

`WithRunMiddleware<T>()` and `WithToolMiddleware<T>()` register middleware as scoped, so it sees the run's services,
such as the current customer. It runs in the order you register it, the first registered outermost:

<!-- snippet: registration -->
<a id='snippet-registration'></a>
```cs
services.AddTypeshift()
    .WithStore<EfTypeshiftStore>()
    .WithChatClientProvider<KeyedChatClientProvider>()
    .WithRealtimeTracking()
    .WithRunMiddleware<BlockedTopics>()
    .WithToolMiddleware<AuditToolCalls>();

services.AddKeyedSingleton("fast-model", fast);
services.AddKeyedSingleton("reasoning-model", reasoning);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L18-L28' title='Snippet source file'>snippet source</a> | <a href='#snippet-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Context providers

### Writing a provider

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

`GetContextAsync` returns an `AgentContext`, and each of its parts is appended to what the run already has:

| Part | Goes |
|---|---|
| `Instructions` | After the agent's instructions |
| `Messages` | Ahead of the input, after the resumed conversation |
| `Tools` | To this run only |

Typeshift asks the provider after it resumes the conversation and before it calls the model. The conversation stores
none of what the provider adds, so every run asks again.

### Using a provider

The agent names its providers in its constructor:

<!-- snippet: use-context -->
<a id='snippet-use-context'></a>
```cs
UseContext<CustomerProfileContext>();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L36-L38' title='Snippet source file'>snippet source</a> | <a href='#snippet-use-context' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A provider needs no registration. Typeshift resolves it from the run's services, or creates it from them when none
is registered, so its constructor can take scoped services.

### Context tools

Context tools are offered like the agent's own, and one wrapped in `ApprovalRequiredAIFunction` asks for
[approval](./tool-approval.md).

::: warning Plan mode hides context tools
Context tools carry no `ReadOnly()` mark, so [plan mode](./tool-approval.md#plan-mode) hides them.
:::

## Related

- [How a run works](./running-agents.md#how-a-run-works): where middleware and context sit among a run's steps.
- [Agents](./defining-agents.md): `UseContext<T>()` among the other calls of an agent's constructor.
- [Tool approval](./tool-approval.md#approval-rules): rules, which decide tool calls before the host is asked.
- [Registration](./registration.md): the other calls of the `AddTypeshift()` chain.
