---
title: Using an agent
description: Get an agent's host from dependency injection and run the agent from a web request, a console app or a background job.
---

# Using an agent

Your app uses an agent through its host, `AgentHost<TAgent>`, which `services.AddTypeshift()` registers for every
agent ([Registration](./registration.md)). You get the host like any other service, from the scope of the request or
job that runs the agent, so the agent's tools see that scope's services. [Running agents](./running-agents.md) covers
what a run takes and returns.

## Inject the host in a web app

In a web app, add the host to the constructor of the class that runs the agent, such as a controller or a service of
your own, or to the parameters of a minimal API endpoint:

<!-- snippet: inject-host -->
<a id='snippet-inject-host'></a>
```cs
internal sealed class RefundDesk(AgentHost<SupportAgent> support, ICurrentCustomer customer)
{
    public async Task<string> AnswerAsync(string question, CancellationToken ct)
    {
        var answer = await support.RunAsync(question, settingsId: customer.Id, ct: ct);
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

Outside a request, such as in a console app, nothing creates a scope for you. Create one from your app's service
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
`ICurrentCustomer` reaches the tools of another.
:::

## Run from a background job

A `BackgroundService` is a singleton, so a host it took in its constructor would come from the root provider. Take
`IServiceScopeFactory` instead, and create a scope for each job:

<!-- snippet: background-host -->
<a id='snippet-background-host'></a>
```cs
internal sealed class TicketDigest(IServiceScopeFactory scopes, TimeProvider time) : BackgroundService
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
        // A scope for each job, so the run gets scoped services of its own, as a request would
        await using var scope = scopes.CreateAsyncScope();
        var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();

        var answer = await support.RunAsync("Summarize the tickets opened today.", ct: ct);
        return answer.Text;
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/TicketDigest.cs#L7-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-background-host' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Register the job with `services.AddHostedService<TicketDigest>()`, and the clock its timer waits on with
`services.AddSingleton(TimeProvider.System)`. A test can register a fake `TimeProvider` instead.

## Related

- [Getting started](./getting-started.md): a first toolset, agent and run.
- [Running agents](./running-agents.md): settings ids, run settings, typed output, streaming and the raw path.
- [Registration](./registration.md#what-addtypeshift-registers): every service `AddTypeshift()` registers and its
  lifetime.
- [Agent host](./generated-members.md#the-agent-host): every member of the host and its signature.
- [Serving a chat endpoint](./chat-endpoint.md): the endpoints a chat UI talks to.
