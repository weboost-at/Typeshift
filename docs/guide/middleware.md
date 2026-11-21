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
the user asks about passwords, and drops such a message when it is [steered](#steering-input) into a run:

<!-- snippet: run-middleware -->
<a id='snippet-run-middleware'></a>
```cs
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
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/BlockedTopics.cs#L5-L28' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-middleware' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

An `IRunMiddleware` receives the run as an `AgentRun`:

| Member | Holds |
|---|---|
| `Agent` | The agent that runs, such as its `Name`. |
| `Messages` | The resumed conversation, then the input, as the pass began. What context providers add and steering input join the run below run middleware, so they are not among them. |
| `Options` | The run's resolved `ChatOptions`, with every layer of settings applied. |
| `Provider` | The [provider](./models.md#registering-clients) that serves the run: the key the call site, the agent's settings or its code name, the default provider's key, or the `ChatProvider` document the call site handed the run; null on the client registered for every agent. Read-only: the run's client is taken before middleware runs, so a model your middleware sets in `Options` goes to this provider. |
| `Mode` | The mode the run is in. |
| `RunOptions` | The run's [run options](./builders.md#runoptionsbuilder), with every layer of settings applied, such as `MaxToolIterations`. |
| `TryGetSettingsId<TId>(out id)` | The settings id the host was given with `WithSettingsId`, read as a `TId`. False without one, or with one of another type. A sub-agent's run has its caller's. |
| `TryGetConversationId<TId>(out id)` | The id of the run's conversation, read as a `TId`. False for a run without a conversation, or with an id of another type. |
| `Services` | The run's services. |
| `Response` | The answer, to read once `next` returns. |

Middleware may change the messages and the options before it calls `next`. Changes to the messages hold for this
run only, because the conversation stores the messages you sent. Middleware runs for every agent, so check `run.Agent`
when it should act on some of them only. Context providers and approval rules receive the same `AgentRun`, so a
memory reads the conversation's id with `run.TryGetConversationId<Guid>(out var conversation)`, typed as your app
gave it to `WithConversationId`.

### Answering in place of the model

Set `run.Response` and skip `next`, as `BlockedTopics` does above, and the middleware answers in place of the model.
The conversation stores that answer. If your middleware returns without calling `next` or setting `run.Response`,
the run throws an `InvalidOperationException`. Set the response in place of calling `next`, never after it
([Guarding the answer](#guarding-the-answer)).

### Steering input

[Steering](./conversations.md#steering) hands input to a run while it works. That input joins the run inside the
tool loop, with the next model call, so `InvokeAsync` never finds it in `run.Messages`. The run shows it to
`OnSteeringAsync` instead, before it takes it: return the input to let it pass, another message to take in its
place, or `null` to drop it. `BlockedTopics` above drops a message about passwords, so the same text is refused
whether it starts a run or is steered into one.

- What `OnSteeringAsync` returns is what the run takes: the model receives that message, and the conversation stores
  it. Input it drops reaches neither, and starts no model call.
- Each middleware sees the input in the order you registered them, and the next one sees what the one before
  returned.
- Input that still waits when a run ends is stored for the next run, and passes `OnSteeringAsync` before it is.
- An exception it throws fails the run. If it throws while the run is ending, the input is not stored, and
  Typeshift logs the failure ([Logs](./observability.md#logs)).
- The answer of a [background sub-agent](./multi-agent.md#in-the-background) is no steering input, and is not shown.

::: warning A guardrail checks steered input too
Unless a middleware implements `OnSteeringAsync`, steered input passes. A guardrail that only checks
`run.Messages` in `InvokeAsync` therefore misses everything a user sends while an answer is under way.
:::

### Streaming runs

The same `InvokeAsync` serves streaming runs. The updates stream on to the caller while `next` runs, and `next`
returns when the stream ends. A response set in place of the model reaches a streaming caller all at once, when
the middleware returns.

::: warning A steered run passes through middleware again
[Steering](./conversations.md#steering) hands new input to a run while it works. Input that arrives after the run's
answer starts another pass of the run, and that pass goes through the middleware again, so middleware can see one run
more than once.
:::

### Guarding the answer

Run middleware guards what goes into a run, and it may answer in place of the model. It cannot change an answer the
model gave. By the time `next` returns, that answer is stored, has streamed to the caller and reached the watchers,
and the next model call reads it from the conversation. `run.Response` is therefore read-only once `next` was
called: setting it then throws an `InvalidOperationException`, where it used to change the answer for the caller of
`RunAsync` alone.

To check or change what a model says, wrap the provider's client in a `DelegatingChatClient`
([Wrapping a client](./models.md#wrapping-a-client)). This one replaces card numbers:

<!-- snippet: output-guardrail -->
<a id='snippet-output-guardrail'></a>
```cs
internal sealed partial class RedactedCardNumbers(IChatClient inner) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        foreach (var text in response.Messages.SelectMany(message => message.Contents).OfType<TextContent>())
        {
            text.Text = CardNumber().Replace(text.Text, "[card number]");
        }

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // A number can arrive split over two updates, so the answer is checked whole and handed on after that
        var response = await GetResponseAsync(messages, options, cancellationToken);

        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    [GeneratedRegex(@"\b\d(?:[ -]?\d){12,18}\b")]
    private static partial Regex CardNumber();
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/RedactedCardNumbers.cs#L7-L42' title='Snippet source file'>snippet source</a> | <a href='#snippet-output-guardrail' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- Everything else takes the answer from your wrapper, so the caller, the stream, the stored conversation, the
  watchers and the next model call all get the text it returned.
- It sees every model call of a run, the ones between tool calls included.
- It answers a streaming call from the whole response, because a card number can arrive split over two updates. The
  caller then gets the answer at once. A check that needs less of the text can hold back less.

Register it where you register the client, or return the wrapped client from your connector:

<!-- snippet: output-guardrail-registration -->
<a id='snippet-output-guardrail-registration'></a>
```cs
services.AddKeyedChatClient("openai", openAi)
    .Use(inner => new RedactedCardNumbers(inner));
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/OutputGuardrail.cs#L17-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-output-guardrail-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Wrap every tool call

An `IToolMiddleware` sees each tool call that runs. This one logs which agent calls which tool for which customer:

<!-- snippet: tool-middleware -->
<a id='snippet-tool-middleware'></a>
```cs
internal sealed class AuditToolCalls(ILogger<AuditToolCalls> logger) : IToolMiddleware
{
    public async ValueTask<object?> InvokeAsync(
        FunctionInvocationContext call,
        Func<ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        var run = call.Run;
        int? customer = run.TryGetSettingsId<int>(out var id) ? id : null;

        logger.LogInformation(
            "{Agent} calls {Tool} for customer {Customer}",
            run.Agent.Name,
            call.Function.Name,
            customer);

        if (call.Function.Name == WorkspaceToolset.ToolNames.RunCommand)
            logger.LogWarning("{Agent} runs {Command}", run.Agent.Name, call.Arguments["command"]);

        return await next();
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/AuditToolCalls.cs#L7-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-tool-middleware' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- It receives Microsoft.Extensions.AI's `FunctionInvocationContext`: the function, its arguments and the messages so
  far. `call.Run` is the run the call belongs to, the `AgentRun` run middleware sees, so the agent and the ids your
  app gave the host come with every call. The customer's id here is the settings id, as in
  [Inject the host in a web app](./using-an-agent.md#inject-the-host-in-a-web-app).
- It also logs the command line of each shell command. It finds those calls by
  `WorkspaceToolset.ToolNames.RunCommand`, the constant the generator writes for the tool, so renaming the tool
  breaks the build here, where a string would stop matching
  ([Names and descriptions](./toolsets.md#names-and-descriptions)).
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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L19-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-registration' title='Start of snippet'>anchor</a></sup>
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
    public ValueTask<AgentContext> GetContextAsync(AgentRun run, CancellationToken cancellationToken) =>
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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L40-L42' title='Snippet source file'>snippet source</a> | <a href='#snippet-use-context' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A provider needs no registration. Typeshift resolves it from the run's services, or creates it from them when none
is registered, so its constructor can take scoped services.

What Typeshift creates, it creates for each pass of a run and disposes once the pass has ended, after the tools the
provider returned have run, if the provider implements `IDisposable` or `IAsyncDisposable`. So a provider that keeps
something from one pass to the next, such as a cache of what it retrieved or a connection it opened, is registered
as scoped with the app's services, `services.AddScoped<T>()` or `typeshift.Services.AddScoped<T>()` in a runtime
without a container. The run then resolves the same instance for each pass, and the scope disposes it.

### Context tools

Context tools, the tools a provider returns in `AgentContext.Tools`, are offered like the agent's own. Wrap one in
`ApprovalRequiredAIFunction`, and its calls wait for [approval](./tool-approval.md).

A context tool needs a name no other tool of the run has. The run fails with an `InvalidOperationException` when a
context tool is named like a tool of the agent, of its sub-agents or of an MCP server, or has a name the run offers
itself, such as `ask_user` ([TSH035](./diagnostics.md#tsh035) lists them). The same holds for the tools of
`WithTools(...)`.

::: warning Plan mode hides context tools
Context tools carry no `ReadOnly()` mark, so [plan mode](./tool-approval.md#plan-mode) hides them. To offer a tool in
plan mode, declare it in a toolset and mark it `ReadOnly()`, or add it to the agent's chat options with
`WithReadOnlyTools(...)`.
:::

## Related

- [How a run works](./how-a-run-works.md): where middleware and context sit among a run's steps.
- [Agents](./defining-agents.md): `UseContext<T>()` among the other calls in an agent's constructor.
- [Approval and modes](./tool-approval.md#approval-rules): rules, which decide tool calls before your app is asked.
- [Registration](./registration.md): the other calls in the `AddTypeshift()` chain.
