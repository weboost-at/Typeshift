---
title: Native AOT
description: Publish a host with Native AOT once your tool types have source-generated JSON metadata.
---

# Native AOT

Typeshift never reflects over your types on generated paths: the generator writes the tool bindings and the settings
serialization. So a host publishes with Native AOT once its tool types have JSON metadata. The analyzer reports each
tool type that lacks it at compile time.

## The project

A host that publishes with Native AOT sets `PublishAot`:

<!-- snippet: aot-project -->
<a id='snippet-aot-project'></a>
```csproj
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>true</InvariantGlobalization>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Typeshift.Samples.Console.csproj#L5-L13' title='Snippet source file'>snippet source</a> | <a href='#snippet-aot-project' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`GenerateDocumentationFile` is there for the tools' descriptions, which the generator reads from each method's
`<summary>` ([TSH011](./diagnostics.md#tsh011)).

## Tool parameter and result types

Outside Native AOT, tool types need nothing. Under Native AOT they need source-generated metadata: a
`JsonSerializerContext` in the toolset's assembly that lists them.

<!-- snippet: json-context -->
<a id='snippet-json-context'></a>
```cs
[JsonSerializable(typeof(IReadOnlyList<Company>))]
[JsonSerializable(typeof(CompanyCount))]
internal partial class AppJsonContext : JsonSerializerContext;
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/AppJsonContext.cs#L6-L10' title='Snippet source file'>snippet source</a> | <a href='#snippet-json-context' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`AddTypeshift()` registers it. Each assembly's generated module adds the contexts of its own, `internal` ones included.
So a toolset library ships its contexts, and the host registers nothing.

Strings, numbers, `bool`, dates, `TimeSpan` and JSON elements need no context. In a project that publishes with Native
AOT or sets `IsAotCompatible`, a tool type no context lists is [TSH027](./diagnostics.md#tsh027).

## Everything else

The generated `Settings` and overrides records serialize member by member, and conversations through
Microsoft.Extensions.AI's own contracts. The built-in toolsets carry a context of their own. So none of these needs a
context from you.

`RunAsync<T>` does: it needs `T` in a context as well, as `CompanyCount` above.

## Related

- [Toolsets](./toolsets.md#results): how a tool's result reaches the model as JSON.
- [Diagnostics](./diagnostics.md#tsh027): TSH027, a tool type without JSON metadata.
- [Running agents](./running-agents.md): structured output with `RunAsync<T>`.
- [Registration](./registration.md#what-addtypeshift-registers): what the generated modules register.
