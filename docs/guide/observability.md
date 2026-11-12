---
title: Observability
description: The OpenTelemetry spans and metrics every run records, how your app listens to them, and when the spans carry your customers' messages.
---

# Observability

Every run records OpenTelemetry spans and metrics, with no Typeshift setting to turn on: you add the `Typeshift`
source and meter to your OpenTelemetry setup. Each run, model call and tool call then shows up as a span, so you can
follow a run from the user's message through every tool call to the answer.

## Listening

Typeshift traces and measures with `System.Diagnostics`, following OpenTelemetry's semantic conventions for
generative AI. Listen to the `Typeshift` source and meter:

<!-- snippet: open-telemetry -->
<a id='snippet-open-telemetry'></a>
```cs
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Typeshift"))
    .WithMetrics(metrics => metrics.AddMeter("Typeshift"));
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L70-L74' title='Snippet source file'>snippet source</a> | <a href='#snippet-open-telemetry' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Spans

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

- [How a run works](./how-a-run-works.md): the steps the spans of a run cover.
- [Wrapping a client](./models.md#wrapping-a-client): why `UseOpenTelemetry()` on your client records each call twice.
- [Conversations](./conversations.md#live-updates): live updates of a run, for a UI.
- [Native AOT](./native-aot.md): publishing your app ahead of time.
