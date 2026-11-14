---
title: Using an agent
description: Get an agent's host and run the agent from a web request, a console app or a background job.
---

# Using an agent

Your app uses an agent through its host, `AgentHost<TAgent>`, which `services.AddTypeshift()` registers for every
agent ([Registration](./registration.md)). A request gets the host like any other service, from its scope, and a
background job takes one from the runtime, which gives the host a scope of its own. Either way, the agent's tools see
the services of that request or job. [Running agents](./running-agents.md) covers what a run takes and returns.

## Inject the host in a web app

A web app registers Typeshift and its chat client once, and a minimal API endpoint takes the agent's host as a
parameter:

<!-- snippet: minimal-api -->
<a id='snippet-minimal-api'></a>
```cs
builder.Services.AddTypeshift();
builder.Services.AddChatClient(client);

var app = builder.Build();

app.MapGet("/research/{city}", async (string city, AgentHost<ResearchAgent> research) =>
    (await research.RunAsync(city)).Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/MinimalApi.cs#L21-L29' title='Snippet source file'>snippet source</a> | <a href='#snippet-minimal-api' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`AddChatClient(client)` registers one client for every agent
([Registering clients](./models.md#registering-clients)). In a class that runs the agent, such as a controller or a
service of your own, add the host to the constructor:

<!-- snippet: inject-host -->
<a id='snippet-inject-host'></a>
```cs
internal sealed class RefundDesk(AgentHost<SupportAgent> support, ICurrentCustomer customer)
{
    public async Task<string> AnswerAsync(string question, CancellationToken ct)
    {
        var answer = await support.WithSettingsId(customer.Id).RunAsync(question, ct: ct);
        return answer.Text;
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/RefundDesk.cs#L5-L14' title='Snippet source file'>snippet source</a> | <a href='#snippet-inject-host' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`ICurrentCustomer` stands for a scoped service of your app, and the customer's id is the settings id, so that customer's
stored settings apply ([Stored settings](./running-agents.md#stored-settings)). Register `RefundDesk` as scoped, with
`services.AddScoped<RefundDesk>()`, so ASP.NET Core creates it in each request's scope, together with its host and that
request's customer. Controllers and endpoint parameters need no registration of their own.

## Run from a console app

A console app or a script without a container creates the runtime with its chat client and takes the host from it:

<!-- snippet: typeshift-runtime -->
<a id='snippet-typeshift-runtime'></a>
```cs
await using var runtime = TypeshiftRuntime.Create(client);
await using var research = runtime.Agent<ResearchAgent>();

var answer = await research.RunAsync("Vienna");

Console.WriteLine($"{research.Agent.Name}: {answer.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/NoContainer.cs#L14-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-typeshift-runtime' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`await using` disposes the host and the runtime when the app is done, and
[The runtime](./registration.md#the-runtime) says what each of them owns.

With a container of your own, nothing creates a scope for you outside a request. Create one from your app's service
provider and resolve the host from it:

<!-- snippet: run -->
<a id='snippet-run'></a>
```cs
await using var scope = provider.CreateAsyncScope();
var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();

var answer = await support.RunAsync("Where is my refund?");

Console.WriteLine($"{answer.Messages[^1].AuthorName}: {answer.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L14-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

::: warning Resolve the host from a scope, never from the root provider
A host resolved from the root provider, such as `app.Services` or what `BuildServiceProvider()` returns, runs with the
root's services. With
[scope validation](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection#scope-validation)
on, as in ASP.NET Core's development environment, the run fails as soon as it resolves a scoped service, such as your
chat client provider. Without it, every run shares one instance of each scoped service, so one customer's
`ICurrentCustomer` reaches the tools of another. The root also keeps every host it creates until your app stops,
since a host is disposable, so each one you resolve there is never released.
:::

## Run from a background job

A `BackgroundService` is a singleton, so a host it took in its constructor would come from the root provider. Take
the `TypeshiftRuntime` in its place, which `AddTypeshift()` registers, and get a host from it for each job:

<!-- snippet: background-host -->
<a id='snippet-background-host'></a>
```cs
internal sealed class TicketDigest(TypeshiftRuntime runtime, TimeProvider time) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1), time);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DigestAsync(stoppingToken);
        }
    }

    public async Task<string> DigestAsync(CancellationToken ct)
    {
        // A host for each job, so the run gets scoped services of its own, as a request would
        await using var support = runtime.Agent<SupportAgent>();

        var answer = await support.RunAsync("Summarize the tickets opened today.", ct: ct);
        return answer.Text;
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/TicketDigest.cs#L6-L28' title='Snippet source file'>snippet source</a> | <a href='#snippet-background-host' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`runtime.Agent<SupportAgent>()` gives the job's host a scope of its own, as a request has, and `await using`
disposes that scope when the job is done ([The runtime](./registration.md#the-runtime)). Register the job with
`services.AddHostedService<TicketDigest>()`. Its timer waits on the `TimeProvider` your app registers, for example
with `services.AddSingleton(TimeProvider.System)`.

## Related

- [Getting started](./getting-started.md): a first toolset, agent and run.
- [Running an agent](./running-agents.md): settings ids, run settings, typed output, streaming and the raw path.
- [Registration](./registration.md#what-addtypeshift-registers): every service `AddTypeshift()` registers and its
  lifetime, and [the runtime](./registration.md#the-runtime).
- [Agent host](./generated-members.md#the-agent-host): every member of the host and its signature.
- [Serving a chat endpoint](./chat-endpoint.md): the endpoints a chat UI talks to.
