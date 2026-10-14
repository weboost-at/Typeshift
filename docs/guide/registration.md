# Registration

`services.AddTypeshift()` registers every toolset and agent of the project and of the projects it references, and
returns `TypeshiftBuilder`, the chain that registers the rest.

## Project setup

```bash
dotnet add package Typeshift --prerelease
```

| Requirement | Why |
|---|---|
| .NET 10 and C# 14 | Generated members are extension blocks. |
| The package in every project that declares toolsets or agents, or calls `AddTypeshift()` | It carries the generator, and NuGet does not pass a generator on to the projects that reference yours ([TSH019](./diagnostics.md#tsh019), [TSH020](./diagnostics.md#tsh020)). |
| `GenerateDocumentationFile` | Tools take their descriptions from XML docs ([TSH011](./diagnostics.md#tsh011)). |
| `Typeshift.Generated` in `InterceptorsNamespaces` | The generator intercepts `AddTypeshift()` from there. The package adds it. |

## The id type

Settings ids and conversation ids are of one type, `Guid` unless `TypeshiftIdType` names another, once for the
whole build in `Directory.Build.props`:

<!-- snippet: id-type -->
<a id='snippet-id-type'></a>
```csproj
<PropertyGroup>
  <TypeshiftIdType>int</TypeshiftIdType>
</PropertyGroup>
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Typeshift.Samples.Docs.csproj#L5-L9' title='Snippet source file'>snippet source</a> | <a href='#snippet-id-type' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A C# keyword (`int`, `long`, `string`), a type of `System`, or a full type name ([TSH029](./diagnostics.md#tsh029)).
Generated members take it (`RunAsync(input, settingsId: 42)`), and `ITypeshiftStore`, `InMemoryTypeshiftStore`,
`ConversationTracker` and `LiveRun` are aliases the generator writes for it. A referenced project built with another
id type is [TSH021](./diagnostics.md#tsh021); a store of another is [TSH028](./diagnostics.md#tsh028). The library
never creates ids.

## What `AddTypeshift()` registers

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

| Service | Lifetime |
|---|---|
| Each toolset | Singleton |
| Each agent | Singleton |
| `AgentHost<TAgent>` per agent | Transient: it runs with the services of the scope it was resolved from |
| The JSON contexts of each assembly, which tools ask before the defaults | Singleton |
| `ConversationTracker` and the core services runs need | Singleton |

The modules come from this project, every project it references that declares toolsets or agents, and the library's
[built-in toolsets](./built-in-toolsets.md). Each registers once per service collection, so calling `AddTypeshift()`
again only returns another chain.

The generator intercepts the call, so it works only in a project that references the package and only as
`services.AddTypeshift()`; a call it did not intercept throws an `InvalidOperationException` saying what to check. A
library that calls it in its own `AddX()` registers its own toolsets and agents and those of its references, not the
host's.

## The chain

| Call | Registers | Lifetime | Needed by |
|---|---|---|---|
| `WithStore<TStore>()` | `TStore`, and `ITypeshiftStore` as it | Singleton | Settings ids, conversation ids, approval, handoffs. Without a store, every call that loads or saves throws, naming this method. |
| `WithChatClientProvider<TProvider>()` | `IChatClientProvider` | Scoped | Every run. |
| `WithRealtimeTracking(RealtimeTrackingOptions? options = null)` | The options: `PublishInterval` (100 ms), `MaxPendingUpdates` (256) | Singleton | `ConversationTracker.WatchAsync`. |
| `WithApprovalRule<TRule>()` | One more `IToolApprovalRule` | Scoped | Deciding calls before the host is asked. |
| `WithRunMiddleware<TMiddleware>()` | One more `IRunMiddleware`; the first registered is the outermost | Scoped | Wrapping every run. |
| `WithToolMiddleware<TMiddleware>()` | One more `IToolMiddleware`; the first registered is the outermost | Scoped | Wrapping every tool call. |
| `WithMcpToolProvider<TProvider>()` | `IMcpToolProvider` | Scoped | Agents with `UseMcpServer`. |
| `WithFileSystemRoot<TRoot>()` | `IFileSystemRoot` | Scoped | `FileSystemToolset`, `ShellToolset`. |
| `WithWebSearch<TSearch>()` | `IWebSearch` | Scoped | `web_search`. |
| `WithWebFetch<TFetch>()` | `IWebFetch` | Scoped | `web_fetch`. |

Scoped services resolve from the run's scope, so they may differ per request: the current customer's credentials,
folder or rules.
