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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/Registration.cs#L50-L54' title='Snippet source file'>snippet source</a> | <a href='#snippet-open-telemetry' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

| Span | For |
|---|---|
| `invoke_agent {agent}` | A run, with the agent, the conversation and the token usage. |
| `chat {model}` | Each model call. |
| `execute_tool {tool}` | Each tool call, with approvals and rejections. |

The metrics are `gen_ai.client.operation.duration` and `gen_ai.client.token.usage`, tagged with the agent and the
model. A store that fails to save a session marks the run's span with the error.

Messages and tool arguments are left out of spans unless `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT` is
`true`.
