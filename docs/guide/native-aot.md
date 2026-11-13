---
title: Native AOT
description: Publish your app with Native AOT, and give the types your tools take and return the JSON metadata they need there.
---

# Native AOT

You can publish your app with Native AOT. Typeshift writes the tool bindings and the settings serialization when you
build, so its generated code never reflects over your types. What you add is JSON metadata for the types your tools
take and return, and the analyzer reports each tool type that lacks it.

## The project

To publish your app with Native AOT, set `PublishAot` in its project. XML docs stay on, as the package turns them on,
so tools keep their descriptions ([TSH011](./diagnostics.md#tsh011)):

<!-- snippet: aot-project -->
<a id='snippet-aot-project'></a>
```csproj
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>true</InvariantGlobalization>
</PropertyGroup>
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Typeshift.Samples.Console.csproj#L5-L11' title='Snippet source file'>snippet source</a> | <a href='#snippet-aot-project' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Tool parameter and result types

Under Native AOT, the types your tools take and return need source-generated metadata: a `JsonSerializerContext` in
the toolset's assembly that lists them. Outside Native AOT, serialization falls back to reflection, so an app that does
not publish with Native AOT needs no context. Set `IsAotCompatible` in a toolset library that such apps use, so the
analyzer checks it.

<!-- snippet: json-context -->
<a id='snippet-json-context'></a>
```cs
[JsonSerializable(typeof(IReadOnlyList<Company>))]
[JsonSerializable(typeof(CompanyCount))]
internal partial class AppJsonContext : JsonSerializerContext;
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/AppJsonContext.cs#L6-L10' title='Snippet source file'>snippet source</a> | <a href='#snippet-json-context' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

You don't register the context yourself: `AddTypeshift()` does. Each assembly's generated module adds that assembly's
contexts, `internal` ones included, so a toolset library ships its own contexts and your app registers nothing for
them.

Strings, `char`, `bool`, `short`, `int`, `long` and their unsigned forms, `float`, `double`, `decimal`, `DateTime`,
`DateTimeOffset`, `TimeSpan`, `IEnumerable<string>` and the JSON element, document and node types need no context.
In a project that publishes with Native AOT or sets `IsAotCompatible`, the analyzer reports
[TSH027](./diagnostics.md#tsh027) for every other tool type that no context lists.

Some of the types that need an entry look as plain as the ones above:

- each nullable value type, such as `int?` or `bool?`, although `int` and `bool` need none. A nullable reference
  type, such as `string?`, counts as its type;
- each enum;
- each array and list, such as `string[]` or `List<string>`;
- `byte`, `Guid` and `DateOnly`.

A tool that returns `IAsyncEnumerable<T>` needs an entry for `List<T>`, because Typeshift sends the stream's items as
one array ([Results](./toolsets.md#results)).

A [union](./toolsets.md#union-types) of two records gets warning SYSLIB1227 in a context, because the source
generator finds no classifier that tells the records apart. Typeshift's tools and runs pick the case by its
properties without one, as [Union types](./toolsets.md#union-types) describes, so you may suppress the warning for
unions only they read. Code of yours that reads such a union still needs a classifier.

## Trackers of value-type ids

The container gives your app a `ConversationTracker<TId>` for each id type it asks for, by closing one open-generic
registration over that type. Under Native AOT, the container refuses to close it over a value type, such as `Guid` or
`int`, because the code for that type may not have been compiled, and resolving the tracker throws an
`InvalidOperationException`. Register the tracker of each value type your app injects in the chain instead:

<!-- snippet: aot-tracker -->
<a id='snippet-aot-tracker'></a>
```cs
services.AddTypeshift()
    .WithStore<InMemoryTypeshiftStore<Guid>>()
    .WithConversationTracker<Guid>()
    .WithChatClientProvider<ScriptedChatClientProvider>();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Program.cs#L10-L15' title='Snippet source file'>snippet source</a> | <a href='#snippet-aot-tracker' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- `WithConversationTracker<Guid>()` registers the tracker of `Guid` ids as a singleton of its own, so the container
  makes no generic type while your app runs.
- The tracker of `string` ids, or of ids of any other reference type, needs no call.
- Inject or resolve the tracker itself, never `IEnumerable<ConversationTracker<Guid>>`: that list holds this tracker
  and the container's own, and under Native AOT resolving it throws.
- Outside Native AOT the container makes every tracker itself, and the call does no harm there, so an app that runs
  both ways keeps it.

## Everything else

The generated `Settings` and overrides records serialize member by member, and conversations go through
Microsoft.Extensions.AI's own contracts, so none of them needs a context from you. `RunAsync<T>`,
`RunStreamingAsync<T>` and a sub-agent's `WithResult<T>()` do need one: list `T` in a context too. In the sample,
`AppJsonContext` above lists `CompanyCount` because the app calls `RunAsync<CompanyCount>`.

::: warning The analyzer checks tool types only
No diagnostic reports a `T` of these calls that no context lists. The build and the publish succeed, and the
published app throws a `NotSupportedException` that names the type when it makes the run.
:::

## Related

- [Toolsets](./toolsets.md#results): how a tool's result reaches the model as JSON.
- [Running an agent](./running-agents.md#structured-output): structured output with `RunAsync<T>` and
  `RunStreamingAsync<T>`.
- [Registration](./registration.md#what-addtypeshift-registers): what the generated modules register.
- [Conversations](./conversations.md#live-runs): the tracker, and the runs it lists.
- [Observability](./observability.md): the spans and metrics every run records.
