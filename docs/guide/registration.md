---
title: Registration
description: What a project needs, the id type, and every service AddTypeshift() and its chain register, with lifetimes.
---

# Registration

`services.AddTypeshift()` registers every toolset and agent of your project and the projects it references. It returns
`TypeshiftBuilder`, the chain that registers the rest.

## Project setup

```bash
dotnet add package Typeshift --prerelease
```

| Requirement | Why |
|---|---|
| .NET 10 and C# 14 | Generated members are extension blocks. |
| The package in every project that declares toolsets or agents, or calls `AddTypeshift()` | The package carries the generator, and NuGet does not pass a generator on to the projects that reference yours ([TSH019](./diagnostics.md#tsh019), [TSH020](./diagnostics.md#tsh020)). |
| `GenerateDocumentationFile` | Tools and the types they take get their descriptions from XML docs ([TSH011](./diagnostics.md#tsh011)). |
| `Typeshift.Generated` in `InterceptorsNamespaces` | The generator intercepts `AddTypeshift()` from that namespace. The package adds it for you. |

## XML docs of parameter types

The package brings [Weboost.SchemaAnnotations](https://github.com/weboost-at/SchemaAnnotations), which turns the XML
docs of your types into descriptions of the properties a tool's parameters hold. It adds a hidden class to every
project with `GenerateDocumentationFile`, and the project's module registers the docs it collects. A project of
contracts with no toolsets or agents gets a module too, so the tools of the projects that reference it describe its
types; it needs the package and `GenerateDocumentationFile` like any other. To leave the docs out, set:

```xml
<TypeshiftSchemaAnnotations>false</TypeshiftSchemaAnnotations>
```

::: warning Delete your own `[SchemaAnnotations]` class
A project that marked a class with `[SchemaAnnotations]` itself now has two, so Weboost.SchemaAnnotations writes no
`AIJsonSchemaCreateOptions.Annotated` and code using it stops compiling. Delete your class, and the one the package
adds brings `Annotated` back.
:::

## The id type

Settings ids and conversation ids share one type across the whole build. It is `Guid` unless `TypeshiftIdType` names
another, set once in `Directory.Build.props`:

<!-- snippet: id-type -->
<a id='snippet-id-type'></a>
```csproj
<PropertyGroup>
  <TypeshiftIdType>int</TypeshiftIdType>
</PropertyGroup>
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Typeshift.Samples.Docs.csproj#L5-L9' title='Snippet source file'>snippet source</a> | <a href='#snippet-id-type' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`TypeshiftIdType` takes a C# keyword (`int`, `long`, `string`), a type of `System`, or a full type name
([TSH029](./diagnostics.md#tsh029)). A public agent needs a public id type, since its host members take ids of it
([TSH006](./diagnostics.md#tsh006)).

Generated members take the id type, as in `RunAsync(input, settingsId: 42)`, and `ITypeshiftStore`,
`InMemoryTypeshiftStore`, `ConversationTracker` and `LiveRun` are aliases the generator writes for it. The library
never creates ids; your code chooses them. A referenced project built with another id type is
[TSH021](./diagnostics.md#tsh021), and a store of another id type is [TSH028](./diagnostics.md#tsh028).

Providers have an id type of their own, `TypeshiftProviderIdType`, which is `string` unless set. See
[the provider id type](./models.md#the-provider-id-type).

## What `AddTypeshift()` registers

A typical registration, with the chain:

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

`AddTypeshift()` itself registers these:

| Service | Lifetime |
|---|---|
| Each toolset | Singleton |
| Each agent | Singleton |
| `AgentHost<TAgent>` per agent | Transient, so it runs with the services of the scope it was resolved from |
| The JSON contexts of each assembly, which tools ask before the defaults | Singleton |
| `ConversationTracker` and the core services runs need | Singleton |

The registrations come in modules from three places: this project, every project it references that declares toolsets
or agents, and the library's [built-in toolsets](./built-in-toolsets.md). Each module registers once per service
collection, so a second call of `AddTypeshift()` only returns another chain.

Because the generator intercepts the call, it works only in a project that references the package, and only written as
`services.AddTypeshift()`. A call the generator did not intercept throws an `InvalidOperationException` that says what
to check.

::: warning A library's call registers the library's agents, not the host's
A library that calls `AddTypeshift()` in its own `AddX()` registers its own toolsets and agents and those of its
references. The host's toolsets and agents need the host's own call.
:::

## The chain

| Call | Registers | Lifetime | Needed by |
|---|---|---|---|
| `WithStore<TStore>()` | `TStore`, and `ITypeshiftStore` as it | Singleton | Settings ids, conversation ids, approval and handoffs. Without a store, every call that loads or saves throws, naming this method. |
| `WithChatClientProvider<TProvider>()` | `TProvider`, and `IChatClientProvider` as it | Scoped | Every run. |
| `WithRealtimeTracking(RealtimeTrackingOptions? options = null)` | The options. By default, `PublishInterval` is 100 ms and `MaxPendingUpdates` 256. | Singleton | `ConversationTracker.WatchAsync`. |
| `WithApprovalRule<TRule>()` | One more `IToolApprovalRule` | Scoped | Deciding calls before the host is asked. |
| `WithRunMiddleware<TMiddleware>()` | One more `IRunMiddleware`. The first registered is the outermost. | Scoped | Wrapping every run. |
| `WithToolMiddleware<TMiddleware>()` | One more `IToolMiddleware`. The first registered is the outermost. | Scoped | Wrapping every tool call. |
| `WithMcpToolProvider<TProvider>()` | `IMcpToolProvider` | Scoped | Agents with `UseMcpServer`. |
| `WithFileSystemRoot<TRoot>()` | `IFileSystemRoot` | Scoped | `FileSystemToolset`, `ShellToolset`. |
| `WithWebSearch<TSearch>()` | `IWebSearch` | Scoped | `web_search`. |
| `WithWebFetch<TFetch>()` | `IWebFetch` | Scoped | `web_fetch`. |

Scoped services resolve from the run's scope, so they can differ per request and use the current customer's
credentials, folder or rules, for example.

## Related

- [Getting started](./getting-started.md): the setup, step by step.
- [Running agents](./running-agents.md): resolving a host from a scope and running an agent.
- [Settings and stores](./settings-and-stores.md): the store `WithStore<T>()` registers.
- [Generated members](./generated-members.md#per-project): the module, the aliases and the interceptor the generator
  writes.
- [Diagnostics](./diagnostics.md): what the analyzer reports when the setup is incomplete.
