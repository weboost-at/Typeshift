---
title: In production
description: The spans and metrics every run records for OpenTelemetry, and what your app needs to publish with Native AOT.
---

# In production

Every run records OpenTelemetry spans and metrics with no Typeshift setting to turn on: you only add the `Typeshift`
source and meter to your OpenTelemetry setup. Your app can publish with Native AOT once the types its tools take and
return have JSON metadata.

## Observability

Typeshift traces and measures every run with `System.Diagnostics`, following OpenTelemetry's semantic conventions for
generative AI. Each run, model call and tool call shows up as a span.

### Listening

Listen to the `Typeshift` source and meter:

<!-- snippet: open-telemetry -->
<a id='snippet-open-telemetry'></a>
```cs
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Typeshift"))
    .WithMetrics(metrics => metrics.AddMeter("Typeshift"));
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L70-L74' title='Snippet source file'>snippet source</a> | <a href='#snippet-open-telemetry' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Spans

| Span | For |
|---|---|
| `invoke_agent {agent}` | A run, with the agent, the requested model, the conversation and the token usage. |
| `chat {model}` | Each model call, including the calls that write a compaction summary. |
| `execute_tool {tool}` | Each tool call. |

The `invoke_agent` span carries these tags:

| Tag | Holds |
|---|---|
| `gen_ai.operation.name` | `invoke_agent` |
| `gen_ai.agent.name`, `gen_ai.agent.description` | The agent's name and description |
| `gen_ai.request.model` | The model the run asks for |
| `gen_ai.conversation.id` | The conversation's id, when the run has one |
| `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens` | The run's token usage |
| `error.type` | The exception's type, when the run fails; the span's status is then an error |

::: warning A failed save does not fail the run
When the store fails to save an entry, the run goes on. Typeshift logs the failure as an error and records the
exception on the run's span, so watch your logs or spans for it.
:::

### Metrics

The metrics are Microsoft.Extensions.AI's `gen_ai.client.operation.duration` and `gen_ai.client.token.usage`, tagged
with the model.

### Message content

Spans leave out messages and tool arguments unless the environment variable
`OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT` is `true`:

```bash
export OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true
```

::: warning Captured content is your customers' data
With the variable set, your traces hold every run's messages and every tool call's arguments, including what your
customers wrote. Turn it on only where your tracing backend may keep that data.
:::

## Native AOT

The generator writes the tool bindings and the settings serialization, so generated code never uses reflection over
your types. What you add is JSON metadata for the types your tools take and return, and the analyzer reports each
type that lacks it at compile time.

### The project

To publish your app with Native AOT, set `PublishAot` in its project. Keep `GenerateDocumentationFile` on, because
tools take their descriptions from XML docs ([TSH011](./diagnostics.md#tsh011)):

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

### Tool parameter and result types

Under Native AOT, the types your tools take and return need source-generated metadata: a `JsonSerializerContext` in
the toolset's assembly that lists them. Outside Native AOT, serialization falls back to reflection, so a project that
neither publishes with Native AOT nor sets `IsAotCompatible` needs no context.

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

### Everything else

The generated `Settings` and overrides records serialize member by member, conversations go through
Microsoft.Extensions.AI's own contracts, and the built-in toolsets carry a context of their own. None of these needs
a context from you. `RunAsync<T>` does need one: list `T` in a context too. In the sample, `AppJsonContext` above
lists `CompanyCount` because the app calls `RunAsync<CompanyCount>`.

## Related

- [How a run works](./running-agents.md#how-a-run-works): the steps the spans of a run cover.
- [Conversations](./conversations.md#live-updates): live updates of a run, for a UI.
- [Toolsets](./toolsets.md#results): how a tool's result reaches the model as JSON.
- [Running agents](./running-agents.md#structured-output): structured output with `RunAsync<T>`.
- [Registration](./registration.md#what-addtypeshift-registers): what the generated modules register.
