---
title: Middleware and context
description: Wrap every run or tool call for logging, guardrails or audits, and add memory, documents or the customer's profile to an agent's runs.
---

# Middleware and context

Middleware wraps every run or tool call of every agent, so you add logging, guardrails or limits in one place. A
context provider, such as a memory, adds instructions, messages or tools only to the runs of the agents that use it.

## Middleware

### Wrap every run

An `IRunMiddleware` sees each run of every agent before the model does. This one answers in place of the model when
the user asks about passwords:

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

An `IRunMiddleware` receives the run as an `AgentRun`:

| Member | Holds |
|---|---|
| `Agent` | The agent that runs, such as its `Name`. |
| `Messages` | What the model receives: the resumed conversation, then the input. |
| `Options` | The run's resolved `ChatOptions`, with every layer of settings applied. |
| `Mode` | The mode the run is in. |
| `Services` | The run's services. |
| `Response` | The answer, once `next` returns. |

Middleware may change the messages and the options before it calls `next`. Changes to the messages hold for this
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

### Wrap every tool call

An `IToolMiddleware` sees each tool call that runs. This one logs which customer calls which tool:

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

- It receives Microsoft.Extensions.AI's `FunctionInvocationContext`: the function, its arguments and the messages so
  far.
- A call that a rule or your app rejects never reaches it. A call that waits for your app reaches it once your
  answer approved it, in the run that carries the answer out.
- What it returns is the result the model receives. Return without calling `next`, and it answers in place of the
  tool.
- An exception the tool throws passes through it. Catch one there and throw a `ToolException` in its place when
  the model should read why the call failed ([When a tool fails](./toolsets.md#when-a-tool-fails)).
- Setting `call.Terminate` ends the tool loop. The model isn't called again with this round's results, and other
  calls of the same round may be skipped.

### Registering

`WithRunMiddleware<T>()` and `WithToolMiddleware<T>()` register middleware as scoped, so it sees the run's services,
such as the current customer. Middleware runs in the order you register it, so the first one you register sees the run
or the call first and finishes last:

<!-- snippet: registration -->
<a id='snippet-registration'></a>
```cs
services.AddTypeshift()
    .WithStore<EfTypeshiftStore>()
    .WithRunMiddleware<BlockedTopics>()
    .WithToolMiddleware<AuditToolCalls>();

services.AddKeyedChatClient("openai", fast);
services.AddKeyedChatClient("anthropic", reasoning);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L18-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The other lines register the store and the chat clients, which [the chain](./registration.md#the-chain) explains.

## Context providers

### Writing a provider

A context provider adds to the runs of the agents that use it, for example the current customer's profile:

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

- [How a run works](./how-a-run-works.md): where middleware and context sit among a run's steps.
- [Agents](./defining-agents.md): `UseContext<T>()` among the other calls in an agent's constructor.
- [Approval and modes](./tool-approval.md#approval-rules): rules, which decide tool calls before your app is asked.
- [Registration](./registration.md): the other calls in the `AddTypeshift()` chain.
