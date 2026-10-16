---
title: Observability
description: The spans and metrics Typeshift records for every run, and how to listen to them with OpenTelemetry.
---

# Observability

Typeshift traces and measures every run through `System.Diagnostics`, following OpenTelemetry's semantic conventions for
generative AI. There is nothing to turn on: listen to the `Typeshift` source and meter. You then see each run, each
model call and each tool call as a span.

::: tip The short version

- Listen to the `Typeshift` source and meter. There is nothing else to turn on.
- A run is an `invoke_agent` span, each model call a `chat` span, each tool call an `execute_tool` span.
- The metrics are the operation duration and the token usage, tagged with the model.
- Messages and tool arguments stay out of spans unless `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT` is `true`.

:::

## Listening

<!-- snippet: open-telemetry -->
<a id='snippet-open-telemetry'></a>
```cs
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Typeshift"))
    .WithMetrics(metrics => metrics.AddMeter("Typeshift"));
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L69-L73' title='Snippet source file'>snippet source</a> | <a href='#snippet-open-telemetry' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Spans

| Span | For |
|---|---|
| `invoke_agent {agent}` | A run, with the agent, the requested model, the conversation and the token usage. |
| `chat {model}` | Each model call, a summary's included. |
| `execute_tool {tool}` | Each tool call. |

The `invoke_agent` span carries these tags:

| Tag | Holds |
|---|---|
| `gen_ai.operation.name` | `invoke_agent` |
| `gen_ai.agent.name`, `gen_ai.agent.description` | The agent's name and description |
| `gen_ai.request.model` | The model the run asks for |
| `gen_ai.conversation.id` | The conversation's id, when the run has one |
| `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens` | The run's token usage |
| `error.type` | The exception's type, when the run fails |

A run that fails marks its span with the error.

::: warning A failed save does not fail the run
When the store fails to save an entry, the run goes on. Typeshift records the exception on the run's span, so watch the
spans for it.
:::

## Metrics

The metrics are Microsoft.Extensions.AI's `gen_ai.client.operation.duration` and `gen_ai.client.token.usage`, tagged
with the model.

## Message content

Spans leave out messages and tool arguments unless the environment variable
`OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT` is `true`:

```bash
export OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true
```

::: warning Captured content is your customers' data
With the variable set, your traces hold every run's messages and every tool call's arguments, including what your
customers wrote. Turn it on only where your tracing backend may keep that data.
:::

## Related

- [How a run works](./running-agents.md#how-a-run-works): the steps the spans of a run cover.
- [Long-running agents](./long-running-agents.md): watching a run that makes hundreds of model calls.
- [Conversations](./conversations.md#live-updates): live updates of a run, for a UI.
