# Middleware

Middleware wraps every run or every tool call of every agent, for logging, guardrails, auditing or limits. It is
resolved from the run's services, so scoped services reach it.

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Hosting/BlockedTopics.cs#L5-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-middleware' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`InvokeAsync` sees the run's agent, messages and options, and may change them before it calls `next`. After `next`,
`run.Response` holds the answer. Middleware that sets `run.Response` without calling `next` answers in place of the
model. The same method serves streaming runs: `next` returns when the stream ends, and a response set in place of
the model is streamed as it is.

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Hosting/AuditToolCalls.cs#L6-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-tool-middleware' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`call` is Microsoft.Extensions.AI's `FunctionInvocationContext`: the function, its arguments, the messages so far.
Return without calling `next` to answer in place of the tool; set `call.Terminate` to end the tool loop.

## Registering

`WithRunMiddleware<T>()` and `WithToolMiddleware<T>()` add middleware in order: the first registered is the
outermost.

<!-- snippet: registration -->
<a id='snippet-registration'></a>
```cs
services.AddTypeshift()
    .WithStore<EfTypeshiftStore>()
    .WithChatClientProvider<KeyedChatClientProvider>()
    .WithRealtimeTracking()
    .WithRunMiddleware<BlockedTopics>()
    .WithToolMiddleware<AuditToolCalls>()
    .WithMcpToolProvider<McpToolProvider>();

services.AddKeyedSingleton("fast-model", fast);
services.AddKeyedSingleton("reasoning-model", reasoning);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/Registration.cs#L15-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->
