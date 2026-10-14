# Observability

Typeshift traces and measures every run through `System.Diagnostics`, following OpenTelemetry's semantic conventions
for generative AI. Nothing to turn on: listen to the `Typeshift` source and meter.

<!-- snippet: open-telemetry -->
<a id='snippet-open-telemetry'></a>
```cs
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Typeshift"))
    .WithMetrics(metrics => metrics.AddMeter("Typeshift"));
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L68-L72' title='Snippet source file'>snippet source</a> | <a href='#snippet-open-telemetry' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

| Span | For |
|---|---|
| `invoke_agent {agent}` | A run, with the agent, the requested model, the conversation and the token usage. |
| `chat {model}` | Each model call, a summary's included. |
| `execute_tool {tool}` | Each tool call. |

The metrics are Microsoft.Extensions.AI's `gen_ai.client.operation.duration` and `gen_ai.client.token.usage`, tagged
with the model. A run that fails marks its span with the error; a store that fails to save an entry records the
exception on the run's span, while the run goes on.

Messages and tool arguments are left out of spans unless `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT` is
`true`.
