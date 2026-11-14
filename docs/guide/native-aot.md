---
title: Native AOT
description: Publish your app with Native AOT or trimmed, and give the types your tools take and return the JSON metadata they need there.
---

# Native AOT

You can publish your app with Native AOT, or trimmed. Typeshift writes the tool bindings and the settings
serialization when you build, so its generated code never reflects over your types. What you add is JSON metadata
for the types your tools take and return, and the analyzer reports each tool type that lacks it.

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

An app published trimmed sets `PublishTrimmed` in place of `PublishAot`. For JSON it is where a Native AOT app is,
because the SDK turns System.Text.Json's reflection off for both. So what this page says about JSON metadata holds
for a trimmed app too.

## Tool parameter and result types

Where System.Text.Json does not reflect, the types your tools take and return need source-generated metadata: a
`JsonSerializerContext` in the toolset's assembly that lists them. That is the case in an app published with Native
AOT and in one published trimmed. Without the metadata such an app builds and publishes, and each tool that takes or
returns a record or a list fails when the model calls it.

An app that keeps JSON reflection needs no context. That is an app published neither way, a Blazor WebAssembly app,
which trims and turns reflection back on, and an app that sets `JsonSerializerIsReflectionEnabledByDefault` to `true`
itself. In a toolset library that Native AOT or trimmed apps use, set `IsAotCompatible` or `IsTrimmable`, so the
analyzer checks the library too.

<!-- snippet: json-context -->
<a id='snippet-json-context'></a>
```cs
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(IReadOnlyList<Company>))]
[JsonSerializable(typeof(CompanyCount))]
[JsonSerializable(typeof(Sector))]
[JsonSerializable(typeof(SectorCount))]
internal partial class AppJsonContext : JsonSerializerContext;
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/AppJsonContext.cs#L7-L14' title='Snippet source file'>snippet source</a> | <a href='#snippet-json-context' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

You don't register the context yourself: `AddTypeshift()` does. Each assembly's generated module adds that assembly's
contexts, `internal` ones included, so a toolset library ships its own contexts and your app registers nothing for
them. A project that holds nothing of Typeshift but a context, such as an app whose agents live in a library, gets a
module for it too.

The module registers a context through its static `Default` member, so it takes the contexts that have one:

- A `partial` context with at least one `[JsonSerializable]`, as the one above. System.Text.Json's source generator
  writes its `Default`.
- A context you write by hand, when it declares a public or internal static `Default` property or field itself.

Any other context is left out, and the types it lists count as not listed for
[TSH027](./diagnostics.md#tsh027). The context derives from `JsonSerializerContext` directly and names it in its base
list.

`UseStringEnumConverter = true` keeps an enum what the model knows it as. Where JSON reflects, a tool's enum is its
name: in the schema the model reads, in the argument it sends and in the result it gets back. Where it does not,
the context decides, and without the option an enum is a number there. The schema then says `integer`, the model
has to guess which number means what, and a name it sends fails the call. The analyzer warns about a context that
lists a tool type with an enum and lacks the option ([TSH033](./diagnostics.md#tsh033)). An enum that carries its
own `[JsonConverter(typeof(JsonStringEnumConverter<TEnum>))]` needs none.

Strings, `char`, `bool`, `short`, `int`, `long` and their unsigned forms, `float`, `double`, `decimal`, `DateTime`,
`DateTimeOffset`, `TimeSpan`, `IEnumerable<string>` and the JSON element, document and node types need no context.
In a project that publishes with Native AOT or trimmed, or that sets `IsAotCompatible` or `IsTrimmable`, the analyzer
reports [TSH027](./diagnostics.md#tsh027) for every other tool type that no context lists.

Some of the types that need an entry look as plain as the ones above:

- each nullable value type, such as `int?` or `bool?`, although `int` and `bool` need none. A nullable reference
  type, such as `string?`, counts as its type;
- each enum, in a context that [writes enums as their names](./diagnostics.md#tsh033);
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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Program.cs#L13-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-aot-tracker' title='Start of snippet'>anchor</a></sup>
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
`AppJsonContext` above lists `CompanyCount` because the app calls `RunAsync<CompanyCount>`. An enum in such a `T`
is a number unless its context has `UseStringEnumConverter` too.

::: warning The analyzer checks tool types only
No diagnostic reports a `T` of these calls that no context lists. The build and the publish succeed, and the
published app throws a `NotSupportedException` that names the type when it makes the run.
:::

A conversation's [decisions](./tool-approval.md#how-approval-works) are stored without a context too. An endpoint of
your own that returns a `Decision` or takes a `DecisionAnswer` needs both in the context your web app serializes
with, as every type of an endpoint does. The two cover every kind of decision and of answer, the questions and their
answers included:

<!-- snippet: decision-json-context -->
<a id='snippet-decision-json-context'></a>
```cs
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(Decision))]
[JsonSerializable(typeof(DecisionAnswer))]
internal partial class EndpointJsonContext : JsonSerializerContext;
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/AppJsonContext.cs#L17-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-decision-json-context' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Related

- [Toolsets](./toolsets.md#results): how a tool's result reaches the model as JSON.
- [Running an agent](./running-agents.md#structured-output): structured output with `RunAsync<T>` and
  `RunStreamingAsync<T>`.
- [Registration](./registration.md#what-addtypeshift-registers): what the generated modules register.
- [Conversations](./conversations.md#live-runs): the tracker, and the runs it lists.
- [Observability](./observability.md): the spans and metrics every run records.
