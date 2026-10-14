---
title: Middleware
description: Wrap every run or every tool call of every agent, for logging, guardrails, auditing or limits.
---

# Middleware

Middleware wraps every run or every tool call of every agent. Use it for logging, guardrails, auditing or limits.
Typeshift resolves it from the run's services, so scoped services, such as the current customer, reach it.

::: tip The short version

- An `IRunMiddleware` wraps a run. It may change the run's messages and options, then calls `next`.
- An `IToolMiddleware` wraps each tool call. What it returns is the result the model receives.
- Either answers in place of the model or the tool when it does not call `next`.
- `WithRunMiddleware<T>()` and `WithToolMiddleware<T>()` register them. The first registered is the outermost.

:::

## Around runs

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

`InvokeAsync` sees the run's agent, messages, options and mode. It may change the messages and the options before it
calls `next`. After `next` returns, `run.Response` holds the answer.

The changes hold for this run only: a conversation keeps the messages the host sent. Middleware runs for every agent, so
check `run.Agent` when it should act on some agents only.

## Answering in place of the model

Middleware that sets `run.Response` without calling `next` answers in place of the model, as `BlockedTopics` does above.
A conversation stores that answer. Middleware that neither calls `next` nor sets `run.Response` fails the run.

## Streaming runs

The same `InvokeAsync` serves streaming runs. The updates stream on to the caller while `next` runs, and `next` returns
when the stream ends. A response set in place of the model is streamed as it is.

::: warning A steered run passes through middleware again
Steering input that arrives after a run's answer starts another pass of the run, and that pass goes through the
middleware again. So middleware can see one run more than once. See [Conversations](./conversations.md).
:::

## Around tool calls

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

`call` is Microsoft.Extensions.AI's `FunctionInvocationContext`: the function, its arguments and the messages so far.
What `InvokeAsync` returns is the result the model receives.

- Return without calling `next` to answer in place of the tool.
- Set `call.Terminate` to end the tool loop.

## Registering

`WithRunMiddleware<T>()` and `WithToolMiddleware<T>()` add middleware, scoped. Middleware runs in the order you register
it: the first registered is the outermost.

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

## Related

- [How a run works](./how-a-run-works.md): where middleware sits among a run's steps, and what each step can change.
- [Context providers](./context-providers.md): adding instructions, messages or tools to every run.
- [Tool approval](./tool-approval.md#approval-rules): rules, which decide tool calls before the host is asked.
- [Registration](./registration.md): the other calls of the `AddTypeshift()` chain.
