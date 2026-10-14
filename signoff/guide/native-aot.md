# Native AOT

Typeshift never reflects over your types on generated paths: the generator writes the tool bindings and the settings
serialization. A host publishes with Native AOT once its tool types have JSON metadata.

## Tool parameter and result types

Outside Native AOT, tool types need nothing. Under Native AOT they need source-generated metadata: a
`JsonSerializerContext` in the toolset's assembly that lists them.

<!-- snippet: json-context -->
<a id='snippet-json-context'></a>
```cs
[JsonSerializable(typeof(IReadOnlyList<Company>))]
[JsonSerializable(typeof(IReadOnlyList<Ticket>))]
[JsonSerializable(typeof(Ticket))]
[JsonSerializable(typeof(IReadOnlyList<Article>))]
[JsonSerializable(typeof(IReadOnlyList<Invoice>))]
[JsonSerializable(typeof(RefundStatus))]
internal partial class SamplesJson : JsonSerializerContext;
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/SamplesJson.cs#L10-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-json-context' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`AddTypeshift()` registers it: each assembly's generated module adds the contexts that list a parameter or result type
of its own tools, so a toolset library ships its contexts, `internal` ones included, and the host registers nothing.
In a project that publishes with Native AOT (or sets `IsAotCompatible`), a tool type no context lists is
[TSH027](./diagnostics.md#tsh027).

## Everything else

The generated `Settings` and overrides records serialize member by member, and the library's own documents use its
own context. `RunAsync<T>` needs `T` in a context as well.
