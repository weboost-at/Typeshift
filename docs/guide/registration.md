---
title: Registration
description: What a project needs, where ids come from, and every service AddTypeshift() and its chain register, with lifetimes.
---

# Registration

`services.AddTypeshift()` registers every toolset and agent of your project and the projects it references. It returns
a `TypeshiftBuilder`, whose `With...` calls register what your app supplies, such as the store and the chat client
provider.

## Project setup

```bash
dotnet add package Typeshift --prerelease
```

| Requirement | Why |
|---|---|
| .NET 11 | The package targets `net11.0`. Generated members are extension blocks, which need C# 14 or later. |
| The package in every project that declares toolsets or agents, or calls `AddTypeshift()` | The package carries the generator, and NuGet does not pass a generator on to the projects that reference yours ([TSH019](./diagnostics.md#tsh019), [TSH020](./diagnostics.md#tsh020)). |
| `GenerateDocumentationFile` | Tools, agents and the types tools take get their descriptions from XML docs. The package turns it on unless your project sets it, and hides CS1591, the warning for undocumented public members, when it does ([TSH011](./diagnostics.md#tsh011)). |
| `Typeshift.Generated` in `InterceptorsNamespaces` | The generator intercepts `AddTypeshift()` from that namespace. The package adds it for you. |

## XML docs of parameter types

The package brings [Weboost.SchemaAnnotations](https://github.com/weboost-at/SchemaAnnotations), which turns the XML
docs of your types into descriptions of their properties, in the schemas of a tool's parameters and of the answer
`RunAsync<T>` and `RunStreamingAsync<T>` ask for. Typeshift adds a hidden class to every project with
`GenerateDocumentationFile` on, as it is unless the project turns it off. Weboost.SchemaAnnotations collects the
project's XML docs in that class, and the project's [registration module](#what-addtypeshift-registers) registers them
when `AddTypeshift()` runs. A project of contracts with no toolsets or agents gets a module too, so the tools of the
projects that reference it describe its types; it needs the package like any other. To leave these
descriptions out, set:

```xml
<TypeshiftSchemaAnnotations>false</TypeshiftSchemaAnnotations>
```

::: warning Delete your own `[SchemaAnnotations]` class
If your project already marks a class with `[SchemaAnnotations]`, adding the Typeshift package gives it a second one.
Weboost.SchemaAnnotations then generates no `AIJsonSchemaCreateOptions.Annotated`, and code that uses it stops
compiling. Delete your own class, and the one the package adds brings `Annotated` back.
:::

## Ids

Your app keeps settings and conversations under ids it already has, such as a customer's id or the key of a chat.
You give them to a host in code, with `support.WithSettingsId(42)` and `support.WithConversationId(chat)`, and the
id's type picks the store of that type, so one app can keep settings under `int` ids and conversations under `Guid`
ids ([Ids of your own types](./settings-and-stores.md#ids-of-your-own-types)).
Typeshift creates an id only when you keep every conversation, for a conversation a run starts without one
([Keeping every conversation](./conversations.md#keeping-every-conversation)).

::: info `TypeshiftIdType` still sets the build's id type
The MSBuild property `TypeshiftIdType` is still accepted, and a coming release removes it. It is `Guid` unless set.
The container has the `ConversationTracker<TId>` of that type only, so a default app's tracker is
`ConversationTracker<Guid>`. Its `TrySteer` and `WatchAsync` take ids of any type, but `Runs` and `WatchRunsAsync`
list only the conversations under ids of the build's type. An app that lists its running conversations under `int`
ids sets the property to `int`, as the docs samples do. The generated members also still take `settingsId` and
`conversationId` parameters of that type, which the same release removes, so give ids with `WithSettingsId` and
`WithConversationId` instead.
:::

Providers have an id type of their own, `TypeshiftProviderIdType`, which is `string` unless set. See
[the provider id type](./models.md#the-provider-id-type).

## The runtime

`TypeshiftRuntime` is the root of Typeshift in your app. It gives you the host of every agent of your project and of
the projects it references. Each host it gives you is one unit of work, such as a job, with scoped services of its
own.

A console tool or a script without a container of its own creates the runtime, and one chat client serves all its
runs:

<!-- snippet: typeshift-runtime -->
<a id='snippet-typeshift-runtime'></a>
```cs
await using var runtime = TypeshiftRuntime.Create(client);
await using var research = runtime.Agent<ResearchAgent>();

var answer = await research.RunAsync("Vienna");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/NoContainer.cs#L11-L16' title='Snippet source file'>snippet source</a> | <a href='#snippet-typeshift-runtime' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- `TypeshiftRuntime.Create(client, typeshift => typeshift.WithStore<MyStore>())` takes the rest of the chain, as
  `AddTypeshift()` does. The generator intercepts the call, so write it as `TypeshiftRuntime.Create(...)`: a call
  through `using static` or a method group is not intercepted and throws.
- Create the runtime once and keep it while your app runs. Disposing it disposes its singletons, such as your
  store, but not the client. A host's scoped services are disposed only with the host, so dispose each host as well.
- An app whose tools or stores need services of its own calls `services.AddTypeshift()` instead, so it can register
  them.

An app with a container gets the runtime from `services.AddTypeshift()`, which registers it as a singleton. Inject it
where no request's scope is at hand, such as in a background service, and take a host from it for each job, as
[Run from a background job](./using-an-agent.md#run-from-a-background-job) shows. Your container owns that runtime,
so disposing it does nothing. A request injects the host itself, from the request's scope
([Using an agent](./using-an-agent.md)).

`runtime.Services` holds the root services, for singletons such as the `ConversationTracker`. Scoped services are
not among them, since each lives in a scope, such as the one a host has.

::: warning Dispose every host the runtime gives you
`runtime.Agent<TAgent>()` creates a scope for the host. The services the chain registers as scoped, such as the
`WithChatClientProvider<T>()` provider, approval rules, middleware and the MCP tool provider, and the scoped services
your tools resolve, are created in it. Every run of that host shares them, and no other host sees them. Disposing the
host disposes the scope and its services, so write `await using`, and let every run and stream of the host finish
first: a stream read after its host was disposed throws an `ObjectDisposedException`. A host injected from a
request's scope owns nothing, and disposing it leaves the request's services alone.
:::

## What `AddTypeshift()` registers

A typical registration, with the chain:

<!-- snippet: registration -->
<a id='snippet-registration'></a>
```cs
services.AddTypeshift()
    .WithStore<EfTypeshiftStore>()
    .WithRunMiddleware<BlockedTopics>()
    .WithToolMiddleware<AuditToolCalls>();

services.AddKeyedSingleton("openai", fast);
services.AddKeyedSingleton("anthropic", reasoning);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L18-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`fast` and `reasoning` are `IChatClient`s your app creates. The last two lines register them under the keys `openai`
and `anthropic`, and a run takes the client registered under its provider
([Registering clients](./models.md#registering-clients)).

`AddTypeshift()` itself registers these:

| Service | Lifetime |
|---|---|
| Each toolset | Singleton |
| Each agent | Singleton |
| `AgentHost<TAgent>` per agent | Transient, so it runs with the services of the scope it was resolved from ([Using an agent](./using-an-agent.md)) |
| `TypeshiftRuntime`, which gives each host a scope of its own ([The runtime](#the-runtime)) | Singleton |
| The JSON contexts of each assembly, which tools ask before the defaults | Singleton |
| `ConversationTracker<TId>` of the build's id type ([Ids](#ids)), and the core services runs need | Singleton |

The generator writes a registration module for each project that declares toolsets or agents. `AddTypeshift()` runs the
modules of the project that calls it and of every project it references that declares toolsets or agents. Each module
registers once per service collection, so a second call to `AddTypeshift()` only returns another chain.

Because the generator intercepts the call, it works only in a project that references the package, and only when you
call it on the service collection, such as `services.AddTypeshift()` or `builder.Services.AddTypeshift()`. A call the
generator did not intercept throws an `InvalidOperationException` that says what to check.

::: warning A library's call registers the library's agents, not your app's
A library that calls `AddTypeshift()` in its own `AddX()` registers its own toolsets and agents and those of its
references. Your app's own toolsets and agents need a call to `AddTypeshift()` in your app.
:::

## The chain

| Call | Registers | Lifetime | Needed by |
|---|---|---|---|
| `WithStore<TStore>()` | `TStore`, and each of `IAgentSettingsStore`, `ISettingsStore<TId>`, `IConversationStore<TId>` and `ITypeshiftStore<TId>` it implements, of any id type, resolving to it. Call it once for each class. | Singleton, so it takes no scoped services ([Implementing a store](./implementing-a-store.md#register-your-store)) | Keeping agents' own settings, settings ids and conversations across restarts. Without any store, they are kept in memory, and the first save logs a warning. |
| `WithStore(store)` | The store you created yourself, as each store interface the instance implements. The container never disposes it. | Singleton, your app's | The same. |
| `WithFileStore(Action<FileStoreOptions>? configure = null)` | The file store as `IAgentSettingsStore`, built by the container with your app's `TimeProvider`. By default, `Folder` is `typeshift` under `AppContext.BaseDirectory`; the settings files lie in its `settings` folder. | Singleton | Agents' own settings, kept in files ([Agent settings](./settings-and-stores.md#agent-settings)). |
| `WithChatClientProvider<TProvider>()` | `TProvider`, and `IChatClientProvider` resolving to it | Scoped | Every run with a provider and a model, before any `IChatClient` you registered ([Registering clients](./models.md#registering-clients)). |
| `WithOptions(options => ...)` | `TypeshiftOptions`, which each call changes further, such as `PersistAllConversations` | Singleton | Keeping the runs without a conversation id ([Keeping every conversation](./conversations.md#keeping-every-conversation)). |
| `WithRealtimeTracking(RealtimeTrackingOptions? options = null)` | The options of live updates, in place of the defaults: `PublishInterval` of 100 ms and `MaxPendingUpdates` of 256. Live updates are on without it. | Singleton | Tuning `ConversationTracker.WatchAsync`. |
| `WithApprovalRule<TRule>()` | One more `IToolApprovalRule` | Scoped | Deciding calls before your app is asked. |
| `WithRunMiddleware<TMiddleware>()` | One more `IRunMiddleware`. The first registered is the outermost. | Scoped | Wrapping every run. |
| `WithToolMiddleware<TMiddleware>()` | One more `IToolMiddleware`. The first registered is the outermost. | Scoped | Wrapping every tool call. |
| `WithMcpToolProvider<TProvider>()` | `IMcpToolProvider` | Scoped | Agents with `UseMcpServer`. |

Scoped services resolve from the run's scope, so they can differ per request and use the current customer's
credentials, folder or rules, for example.

## Related

- [Getting started](./getting-started.md): the setup, step by step.
- [Using an agent](./using-an-agent.md): injecting a host or resolving it from a scope.
- [Running agents](./running-agents.md): running an agent.
- [Settings and stores](./settings-and-stores.md): the store `WithStore<T>()` registers.
- [Generated members](./generated-members.md#per-project): the module and the interceptor the generator writes.
- [Diagnostics](./diagnostics.md): what the analyzer reports when the setup is incomplete.
