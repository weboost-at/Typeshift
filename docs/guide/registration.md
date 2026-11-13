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
| .NET 11 | The package targets `net11.0`, and a project on another framework fails at restore with NU1202, which names `net11.0`. Generated members are extension blocks, which need C# 14 or later, the default on .NET 11. A project that pins an earlier `<LangVersion>` gets no generated code and [TSH031](./diagnostics.md#tsh031). |
| The package in every project that declares toolsets or agents, or calls `AddTypeshift()` or `TypeshiftRuntime.Create(...)` | The package carries the generator. The .NET 11 SDK also passes it on to the projects that reference yours, but NuGet's documented default keeps a package's analyzers private, and a reference with `PrivateAssets="all"` or to a compiled DLL stops it. A direct reference is the setup you can rely on ([TSH019](./diagnostics.md#tsh019), [TSH020](./diagnostics.md#tsh020)). |
| `GenerateDocumentationFile` | Tools, agents and the types tools take get their descriptions from XML docs. The package turns it on unless your project sets it ([TSH011](./diagnostics.md#tsh011)). When the package turned it on, it hides CS1591, the warning for undocumented public members, the compiler's other warnings about XML comments never fail a build that treats warnings as errors, and the XML file stays in `obj`: it is not copied, published or packed. A project that sets the property keeps all of that as it chose. |
| `Typeshift.Generated` in `InterceptorsNamespaces` | The generator intercepts `AddTypeshift()` from that namespace. The package adds it for you. |

While the SDK passes the package on, a project that references yours gets the package's build settings along with
the generator. Its XML docs are turned on and CS1591 is hidden, unless it sets `GenerateDocumentationFile` itself, and
with XML docs on it gets a [registration module](#what-addtypeshift-registers) of its own.

## What `AddTypeshift()` registers

A typical registration, with the chain:

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
| `ConversationTracker<TId>` of each id type your app asks for ([Live runs](./conversations.md#live-runs)), and the core services runs need | Singleton |

The generator writes a registration module for each project that declares toolsets or agents, and for each project
whose XML docs the package collects, which is every project that leaves them on
([XML docs of parameter types](#xml-docs-of-parameter-types)). A module registers its project's toolsets, agents,
JSON contexts and XML docs. `AddTypeshift()` runs the module of the project that calls it and the modules of the
projects it references. Each module registers once per service collection, so a second call to `AddTypeshift()` only
returns another chain.

Because the generator intercepts the call, it works only in a project that references the package, and only when you
call it on the service collection, such as `services.AddTypeshift()` or `builder.Services.AddTypeshift()`. A call the
generator did not intercept throws an `InvalidOperationException` that says what to check.

::: warning A library's call registers the library's agents, not your app's
A library that calls `AddTypeshift()` in its own `AddX()` registers its own toolsets and agents and those of its
references. Your app's own toolsets and agents need a call to `AddTypeshift()` in your app.
:::

## The chain

| Call | Registers | Lifetime |
|---|---|---|
| `WithStore<TStore>()` | `TStore`, as each store interface it implements | Singleton |
| `WithStore(store)` | A store you created yourself, as each store interface its declared type implements | Singleton, your app's |
| `WithFileStore(configure)` | The file store, as `IAgentSettingsStore` | Singleton |
| `WithChatClientProvider<TProvider>()` | `TProvider`, as each `IChatClientProvider<T>` it implements | Scoped |
| `WithOptions(options => ...)` | `TypeshiftOptions`, such as `PersistAllConversations` | Singleton |
| `WithRealtimeTracking(options)` | `RealtimeTrackingOptions`, the limits of live updates | Singleton |
| `WithConversationTracker<TId>()` | The `ConversationTracker<TId>` of one id type | Singleton |
| `WithApprovalRule<TRule>()` | One more `IToolApprovalRule`, which decides calls before your app is asked | Scoped |
| `WithRunMiddleware<TMiddleware>()` | One more `IRunMiddleware`, which wraps every run | Scoped |
| `WithToolMiddleware<TMiddleware>()` | One more `IToolMiddleware`, which wraps every tool call | Scoped |
| `WithMcpToolProvider<TProvider>()` | The `IMcpToolProvider` of agents with `UseMcpServer` | Scoped |

- A store keeps agents' own settings, settings ids, conversations and their decisions across restarts. Without any
  store, they are kept in memory, and the first save logs a warning. `WithStore<TStore>()` registers your class as
  each of `IAgentSettingsStore`, `ISettingsStore<TId>`, `IConversationStore<TId>`, `IDecisionStore<TId>` and
  `ITypeshiftStore<TId>` it implements, of any id type, so call it once for each class. All of them resolve to one instance, a singleton, so a store takes
  no scoped services ([Implementing a store](./implementing-a-store.md#register-your-store)). The container never
  disposes a store you pass to `WithStore(store)`.
- The file store keeps agents' own settings in files
  ([Keep them in a file](./settings-and-stores.md#keep-them-in-a-file)). `WithFileStore` takes an optional
  `Action<FileStoreOptions>`, and the container builds the store with your app's `TimeProvider`. By default,
  `Folder` is `typeshift` under `AppContext.BaseDirectory`, and the settings files lie in its `settings` folder.
- A chat client provider serves every run with a provider and a model of its type, before any `IChatClient` you
  registered ([Registering clients](./models.md#registering-clients)). One class may implement
  `IChatClientProvider<T>` for several provider types, and for each type the last call wins.
- Each `WithOptions` call changes the same `TypeshiftOptions` further. `PersistAllConversations` keeps the runs
  without a conversation id ([Keeping every conversation](./conversations.md#keeping-every-conversation)).
- Live updates are on without `WithRealtimeTracking`. Its optional `RealtimeTrackingOptions` replace the defaults
  that `ConversationTracker<TId>.WatchAsync` follows: a `PublishInterval` of 100 ms and `MaxPendingUpdates` of 256.
- `WithConversationTracker<TId>()` registers the closed tracker as a singleton of its own. An app published with
  Native AOT needs it to inject the tracker of a value type, such as `Guid` or `int`
  ([Native AOT](./native-aot.md#trackers-of-value-type-ids)).
- Run middleware and tool middleware wrap in the order you register them, so the first registered is the outermost.

Scoped services resolve from the run's scope, so they can differ per request and use the current customer's
credentials, folder or rules, for example.

## Ids

Your app keeps settings and conversations under ids it already has, such as a customer's id or the key of a chat.
You give them to a host in code, with `support.WithSettingsId(42)` and `support.WithConversationId(chat)`, and the
id's type picks the store of that type, so one app can keep settings under `int` ids and conversations under `Guid`
ids ([Ids of your own types](./settings-and-stores.md#ids-of-your-own-types)).
Typeshift creates an id only when you keep every conversation, for a conversation a run starts without one
([Keeping every conversation](./conversations.md#keeping-every-conversation)).

Providers have an id type of their own, which each agent takes from the provider its `WithModel` names: `string`
for `WithModel("openai", "gpt-5-mini")`, or an enum of yours. See
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

Console.WriteLine($"{research.Agent.Name}: {answer.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/NoContainer.cs#L11-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-typeshift-runtime' title='Start of snippet'>anchor</a></sup>
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

`runtime.Services` holds the root services, for singletons such as a `ConversationTracker<TId>`. Scoped services are
not among them, since each lives in a scope, such as the one a host has.

::: warning Dispose every host the runtime gives you
`runtime.Agent<TAgent>()` creates a scope for the host. The services the chain registers as scoped, such as the
`WithChatClientProvider<T>()` provider, approval rules, middleware and the MCP tool provider, and the scoped services
your tools resolve, are created in it. Every run of that host shares them, and no other host sees them. Disposing the
host disposes the scope and its services, so write `await using`, and let every run and stream of the host finish
first: a stream read after its host was disposed throws an `ObjectDisposedException`. A host injected from a
request's scope owns nothing, and disposing it leaves the request's services alone.
:::

## XML docs of parameter types

The package brings [Weboost.SchemaAnnotations](https://github.com/weboost-at/SchemaAnnotations), which turns the XML
docs of your types into descriptions of their properties, in the schemas of a tool's parameters and of the answer
`RunAsync<T>` and `RunStreamingAsync<T>` ask for. Typeshift adds a hidden class to every project with
`GenerateDocumentationFile` on, as it is unless the project turns it off. Weboost.SchemaAnnotations collects the
project's XML docs in that class, and the project's [registration module](#what-addtypeshift-registers) registers them
when `AddTypeshift()` runs. A project of contracts with no toolsets or agents gets a module too, so the tools of the
projects that reference it describe its types; it needs the package like any other.

Weboost.SchemaAnnotations collects the docs of every documented type and property of the project, internal ones
included, whether a tool takes the type or not. Their text is compiled into the project's assembly, which grows by
it, and whoever has the assembly can read it. To leave these descriptions out, set:

```xml
<TypeshiftSchemaAnnotations>false</TypeshiftSchemaAnnotations>
```

::: warning A documented `[Obsolete]` or `[Experimental]` type fails the build
The collected docs name each documented type in a generated file under `obj`. For a type marked `[Obsolete]` that is
the warning CS0618, an error in a build that treats warnings as errors. For a type marked `[Experimental]` it is an
error under the type's own diagnostic ID, in any build. No `#pragma` of yours reaches the generated file. Until
Weboost.SchemaAnnotations handles such types, set `TypeshiftSchemaAnnotations` to `false` in that project, or remove
the type's XML docs. An obsolete member of a type that is not obsolete itself is fine.
:::

::: warning Delete your own `[SchemaAnnotations]` class
If your project already marks a class with `[SchemaAnnotations]`, adding the Typeshift package gives it a second one.
Weboost.SchemaAnnotations then generates no `AIJsonSchemaCreateOptions.Annotated`, and code that uses it stops
compiling. Delete your own class, and the one the package adds brings `Annotated` back.
:::

## Related

- [Getting started](./getting-started.md): the setup, step by step.
- [Using an agent](./using-an-agent.md): injecting a host or resolving it from a scope.
- [Running an agent](./running-agents.md): what a run takes and returns.
- [Implementing a store](./implementing-a-store.md#register-your-store): the store `WithStore<T>()` registers, and the
  memory without one.
- [Generated members](./generated-members.md#per-project): the module and the interceptor the generator writes.
- [Diagnostics](./diagnostics.md): what the analyzer reports when the setup is incomplete.
