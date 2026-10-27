---
title: Middleware and context
description: Wrap every run or tool call for logging, guardrails or audits, and add memory, documents or the customer's profile to an agent's runs.
---

# Middleware and context

Middleware wraps every run or tool call of every agent, so you add logging, guardrails or limits in one place. A
context provider, such as a memory, adds instructions, messages or tools only to the runs of the agents that use it.

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
before it calls `next`, and once `next` returns, `run.Response` holds the answer. Changes to the messages hold for this
run only, because the conversation stores the messages you sent. Middleware runs for every agent, so check `run.Agent`
when it should act on some of them only.

### Answering in place of the model

Set `run.Response` and skip `next`, as `BlockedTopics` does above, and the middleware answers in place of the model.
The conversation stores that answer. If your middleware returns without calling `next` or setting `run.Response`,
the run throws an `InvalidOperationException`.

### Streaming runs

The same `InvokeAsync` serves streaming runs. The updates stream on to the caller while `next` runs, and `next`
returns when the stream ends. A response set in place of the model reaches a streaming caller all at once, when
the middleware returns.

::: warning A steered run passes through middleware again
[Steering](./conversations.md#steering) hands new input to a run while it works. Input that arrives after the run's
answer starts another pass of the run, and that pass goes through the middleware again, so middleware can see one run
more than once.
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

An `IToolMiddleware` wraps every tool call that runs and receives Microsoft.Extensions.AI's `FunctionInvocationContext`:
the function, its arguments and the messages so far. A call that a rule or your app rejects never reaches it. What it
returns is the result the model receives, so returning without calling `next` answers in place of the tool. Setting
`call.Terminate` ends the tool loop: the model is not called again with this round's results, and other calls of the
same round may be skipped.

### Registering

`WithRunMiddleware<T>()` and `WithToolMiddleware<T>()` register middleware as scoped, so it sees the run's services,
such as the current customer. Middleware runs in the order you register it, so the first one you register sees the run
or the call first and finishes last:

<!-- snippet: registration -->
<a id='snippet-registration'></a>
```cs
services.AddTypeshift()
    .WithStore<EfTypeshiftStore>()
    .WithChatClientProvider<KeyedChatClientProvider>()
    .WithRealtimeTracking()
    .WithRunMiddleware<BlockedTopics>()
    .WithToolMiddleware<AuditToolCalls>();

services.AddKeyedSingleton("openai", fast);
services.AddKeyedSingleton("anthropic", reasoning);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L18-L28' title='Snippet source file'>snippet source</a> | <a href='#snippet-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The other lines register the store, the chat clients and live updates, which
[Registration](./registration.md#what-addtypeshift-registers) explains.

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

| Part | Where it goes |
|---|---|
| `Instructions` | After the agent's instructions |
| `Messages` | Ahead of the input, after the resumed conversation |
| `Tools` | After the agent's tools, for this run only |

Typeshift asks the provider after it resumes the conversation and before it calls the model. The conversation stores
none of what the provider adds, so Typeshift asks the provider again on every run.

### Using a provider

The agent names its providers in its constructor:

<!-- snippet: use-context -->
<a id='snippet-use-context'></a>
```cs
UseContext<CustomerProfileContext>();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L38-L40' title='Snippet source file'>snippet source</a> | <a href='#snippet-use-context' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A provider needs no registration. Typeshift resolves it from the run's services, or creates it from them when none
is registered, so its constructor can take scoped services.

### Context tools

Context tools, the tools a provider returns in `AgentContext.Tools`, are offered like the agent's own. Wrap one in
`ApprovalRequiredAIFunction`, and its calls wait for [approval](./tool-approval.md).

::: warning Plan mode hides context tools
Context tools carry no `ReadOnly()` mark, so [plan mode](./tool-approval.md#plan-mode) hides them. To offer a tool in
plan mode, declare it in a toolset and mark it `ReadOnly()`.
:::

## Related

- [How a run works](./running-agents.md#how-a-run-works): where middleware and context sit among a run's steps.
- [Agents](./defining-agents.md): `UseContext<T>()` among the other calls in an agent's constructor.
- [Approval and modes](./tool-approval.md#approval-rules): rules, which decide tool calls before your app is asked.
- [Registration](./registration.md): the other calls in the `AddTypeshift()` chain.
