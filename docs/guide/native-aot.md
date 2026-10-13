# Native AOT

Typeshift never reflects over your types: the generator writes the tool bindings and the settings serialization, and
all other JSON goes through `JsonTypeInfo`. A host publishes with Native AOT once its tool types are described.

## Tool parameter and result types

Arguments and results of your tools are serialized with the options of `AgentJson`, so their types need
source-generated metadata. Declare a `JsonSerializerContext` in the assembly of the tools, listing their parameter and
result types:

<!-- snippet: json-context -->
<a id='snippet-json-context'></a>
```cs
// Tool parameters and results serialize through source-generated metadata, which keeps the sample AOT-safe
[JsonSerializable(typeof(IReadOnlyList<Company>))]
internal partial class AppJsonContext : JsonSerializerContext;
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/AppJsonContext.cs#L6-L10' title='Snippet source file'>snippet source</a> | <a href='#snippet-json-context' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`AddTypeshift()` registers it: the generated module of each assembly adds every context that lists a parameter or
result type of its own tools, where a tool returning `Task<T>` or `ValueTask<T>` has the result type `T`. A toolset
library ships its contexts this way, `internal` ones included, and the host registers nothing.

- A context counts when its `[JsonSerializable]` names a tool type exactly: listing `Company` does not count for a
  tool that returns `IReadOnlyList<Company>`. A context that names no tool type, one for an HTTP API say, is left out,
  so its options never apply to tool JSON.
- A `private` or `protected` nested context is left out, since the module cannot name it.
- A context the generator does not register, one in a host without toolsets for example, goes into
  `AgentJsonOptions.TypeInfoResolverChain` through `services.Configure<AgentJsonOptions>(...)`.

The resolvers are asked in order: those the host configures, the contexts of the host's own tools, those of
referenced assemblies, the library's own context, then the defaults of Microsoft.Extensions.AI. A type that no
context describes compiles, then fails at runtime under Native AOT.

## The project

<!-- snippet: aot-project -->
<a id='snippet-aot-project'></a>
```csproj
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <RootNamespace>Typeshift.Samples</RootNamespace>
  <TargetFramework>net10.0</TargetFramework>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>true</InvariantGlobalization>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Typeshift.Samples.Console.csproj#L5-L14' title='Snippet source file'>snippet source</a> | <a href='#snippet-aot-project' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The console sample publishes this way in CI and fails the build on any trim or AOT warning:

```shell
dotnet publish samples/Typeshift.Samples.Console -c Release -r linux-x64
```

Settings and stored documents need nothing: the generated `Settings` and overrides records serialize member by member,
and `StoredConversation` uses the library's own context.
