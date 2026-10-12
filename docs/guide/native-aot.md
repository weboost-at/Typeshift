# Native AOT

Typeshift never reflects over your types: the generator writes the tool bindings and the settings serialization, and
all other JSON goes through `JsonTypeInfo`. A host publishes with Native AOT once it describes its own tool types.

## Tool parameter and result types

Arguments and results of your tools are serialized with the options of `AgentJson`, so their types need
source-generated metadata. Declare a `JsonSerializerContext` with every parameter and result type of your tools:

<!-- snippet: json-context -->
<a id='snippet-json-context'></a>
```cs
// Tool parameters and results serialize through source-generated metadata, which keeps the sample AOT-safe
[JsonSerializable(typeof(IReadOnlyList<Company>))]
internal partial class AppJsonContext : JsonSerializerContext;
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/AppJsonContext.cs#L6-L10' title='Snippet source file'>snippet source</a> | <a href='#snippet-json-context' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

and add it to the resolver chain:

<!-- snippet: json-registration -->
<a id='snippet-json-registration'></a>
```cs
services.Configure<AgentJsonOptions>(options => options.TypeInfoResolverChain.Add(AppJsonContext.Default));
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Program.cs#L19-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-json-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Your resolvers come first, then the library's own context, then the defaults of Microsoft.Extensions.AI. A type that
no context describes compiles, then fails at runtime under Native AOT.

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
