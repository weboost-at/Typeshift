---
title: Observability
description: The OpenTelemetry spans and metrics every run records, the events Typeshift logs, how your app listens to them, and when the spans carry your customers' messages.
---

# Observability

Every run records OpenTelemetry spans and metrics, with no Typeshift setting to turn on: you add the `Typeshift`
source and meter to your OpenTelemetry setup. Each run, model call and tool call then shows up as a span, so you can
follow a run from the user's message through every tool call to the answer. What a run survives, such as a save that
failed, Typeshift writes to your app's log, each as an event with an id of its own.

## Listening

Typeshift traces and measures with `System.Diagnostics`, following OpenTelemetry's
[semantic conventions for generative AI](https://github.com/open-telemetry/semantic-conventions-genai). Listen to the
`Typeshift` source and meter:

<!-- snippet: open-telemetry -->
<a id='snippet-open-telemetry'></a>
```cs
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Typeshift"))
    .WithMetrics(metrics => metrics.AddMeter("Typeshift"));
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L80-L84' title='Snippet source file'>snippet source</a> | <a href='#snippet-open-telemetry' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Spans

| Span | For |
|---|---|
| `invoke_agent {agent}` | A run, with the agent, the requested model, the conversation and the token usage. |
| `chat {model}` | Each model call, including the calls that write a compaction summary. |
| `execute_tool {tool}` | Each tool call. |

A run's `chat` and `execute_tool` spans are children of its `invoke_agent` span, whether the run streams or not. The
`invoke_agent` span itself is a child of the span that is current where your app starts the run, which for a
streaming run is where it reads the first update. A run inside a web request therefore shows up as one subtree of
that request's trace, and a run in a background job is a trace of its own.

A [handoff](./multi-agent.md#handoff) shows as a second `invoke_agent` span, of the agent handed to, inside the span
of the agent that handed off. A later run of that conversation has one `invoke_agent` span, of the agent that
answers, whichever agent's host your app started it on. A host loads the conversation before the span opens, so a
span your store writes for that load is not its child.

The `invoke_agent` span carries these tags:

| Tag | Holds |
|---|---|
| `gen_ai.operation.name` | `invoke_agent` |
| `gen_ai.agent.name`, `gen_ai.agent.description` | The agent's name and description |
| `gen_ai.request.model` | The model the run asks for |
| `gen_ai.conversation.id` | The conversation's id, when the run has one |
| `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens` | The run's token usage |
| `typeshift.decisions.asked` | The number of [decisions](#decisions) the run asked your app, when it asked any |
| `error.type` | The exception's type, when the run fails; the span's status is then an error |

::: warning A failed save does not fail the run
When the store fails to save an entry, the run goes on. Typeshift logs the failure as an error and records the
exception on the run's span, so watch your logs or spans for it. Only what a run asks your app must be saved: a run
that could not store it fails, with the store's exception on its span.
:::

A compaction summary that fails does not fail the run either: Typeshift logs a warning, records the exception on the
run's span, and goes on as [The summary model](./models.md#the-summary-model) describes.

A tool call that throws does not fail the run either, until [too many rounds in a row](./toolsets.md#when-a-tool-fails)
fail. Its `execute_tool` span is an error, with the exception's type in `error.type` and its message as the status
description, and the tool loop logs the exception as an error. The span and the log are where a tool's exception
goes in full: the model reads its message only when the tool threw a `ToolException`. A background sub-agent that
fails is logged as an error and recorded on the calling run's span.

A `chat` span names the backend that answered its call. Its `gen_ai.provider.name`, `server.address` and
`server.port` come from the `ChatClientMetadata` of the client the run got, which each provider's client library
fills in. When [your own provider](./models.md#your-own-provider) returns a client per customer, each customer's
calls therefore show that customer's endpoint, also when all of them run under one provider id.

## Decisions

A run that ends asking your app is no failure. Its span has no error and no `execute_tool` span for the calls that
wait; those appear under the run that carries your app's answers out
([Approval and modes](./tool-approval.md#how-approval-works)). What the run asks, and what becomes of it, are events
on the `invoke_agent` spans:

| Event | Added by | For |
|---|---|---|
| `typeshift.decision.asked` | The run that asks | Each decision: a tool call that needs approval, a plan, or questions to the user |
| `typeshift.decision.settled` | The run that carries the answers out, or the one whose input passed the decisions by | Each call of the model response that asked, the calls that waited beside a decision included |

Both events name the call in `gen_ai.tool.name` and `gen_ai.tool.call.id`. A call with a decision also has the
decision's id in `typeshift.decision.id`, the id your app answers by, and its kind in `typeshift.decision.kind`:
`approval`, `plan` or `question`. A settled call has `typeshift.decision.outcome`:

| Outcome | Means |
|---|---|
| `approved` | Your app approved the call or the plan. |
| `rejected` | Your app rejected the call or the plan, or an approval rule rejected the call. |
| `answered` | Your app answered the questions. |
| `declined` | Your app declined the questions. |
| `run` | The call needed no decision and waited beside one. It runs with the answers. |
| `expired` | New input passed the decisions by, so the call did not run. |
| `cancelled` | Your app withdrew the decisions with `CancelDecisionsAsync`, so the call did not run. |
| `unknown` | An earlier run began to carry the answers out and stopped before the call's result was stored. |

An `unknown` outcome is worth an alert, because nobody knows whether that call took effect
([When a decision closes](./tool-approval.md#when-a-decision-closes)).

## Logs

Typeshift writes its warnings and errors to the `ILoggerFactory` of your app's container, so an ASP.NET Core app or a
generic host sees them without any setup. A runtime that `TypeshiftRuntime.Create` made has no logger until you
register one in the chain, with `typeshift.Services.AddLogging(...)`
([The runtime](./registration.md#the-runtime)). Without a logger nothing is logged, and the spans above are the only
trace of a failed save or a tool's exception.

Every event has the category `Typeshift`, and an id and a name that stay the same from one version to the next.
Filter and alert on those, since the text of a message may change:

| Id | Name | Level | Logged when |
|---|---|---|---|
| 1 | `NoStore` | Warning | The first save or append goes to memory, because no store is registered ([Which store serves a call](./implementing-a-store.md#which-store-serves-a-call)). Logged once. |
| 2 | `StepNotStored` | Error | The store failed to append a step of a conversation. The entry holds the store's exception, and the run goes on. |
| 3 | `DecisionsNotClosed` | Error | The store failed to save that a conversation's decisions were carried out. The entry holds the exception, and the conversation's next run saves it again. |
| 4 | `ContentNotStored` | Warning | A step is stored without its contents of a type Microsoft.Extensions.AI cannot write ([What is stored](./conversations.md#what-is-stored)). Logged once for each type in a run. |
| 5 | `ContentNotRead` | Warning | A run loaded a conversation with contents this version of Microsoft.Extensions.AI cannot read, and goes on without them. The entry holds their number. |
| 6 | `BackgroundAgentFailed` | Error | A [background sub-agent](./multi-agent.md#in-the-background) failed with an exception the model does not read. The entry holds the exception. |
| 7 | `SummaryFailed` | Warning | A call that writes a compaction summary failed. The entry holds the exception, the model and what the run does next ([The summary model](./models.md#the-summary-model)). |
| 8 | `CallWithoutVerdict` | Error | A call of a tool that requires approval reached the tool loop without a decision about it, so it did not run. A chat client that hides a tool loop of its own causes it ([Wrapping a client](./models.md#wrapping-a-client)). |
| 9 | `UpdatesDropped` | Warning | A watcher of [live updates](./conversations.md#live-updates) fell behind and lost its oldest updates. Logged once for each watcher. |
| 10 | `RunResolved` | Debug | A run got its chat client. The entry holds the agent, the model and the provider it resolved, the client's type, and the settings that applied, in their order. |
| 11 | `Compacted` | Debug | [Compaction](./conversations.md#compaction) shrank a model call. The entry holds the messages and the estimated tokens before and after. |
| 12 | `RuleDecided` | Debug | An [approval rule](./tool-approval.md#approval-rules) approved or rejected a call. The entry holds the rule's type, the tool and the call's id. |
| 13 | `FileStoreWaiting` | Debug | A save of the [file store](./settings-and-stores.md#keep-them-in-a-file) waits for a lock file that another save holds. |
| 14 | `FileStoreFolder` | Information | The file store saved for the first time. The entry holds the folder that keeps the settings. |

The `Debug` events answer why a run used a model or ran a tool without asking. Turn them on for the category, for
example in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Typeshift": "Debug"
    }
  }
}
```

A tool's exception is logged by the tool loop, which is Microsoft.Extensions.AI's, under the category
`Microsoft.Extensions.AI.FunctionInvokingChatClient` ([When a tool fails](./toolsets.md#when-a-tool-fails)).

## Metrics

The meter records one measurement per model call and none per run, so a run that calls the model three times
records three durations. Its four instruments are the histograms of Microsoft.Extensions.AI's OpenTelemetry client:

| Instrument | Unit | Recorded for |
|---|---|---|
| `gen_ai.client.operation.duration` | `s` | Every model call |
| `gen_ai.client.token.usage` | `{token}` | Every model call whose response reports its token usage, once for the input and once for the output tokens |
| `gen_ai.client.operation.time_to_first_chunk` | `s` | Every model call that streams, as those of `RunStreamingAsync` do |
| `gen_ai.client.operation.time_per_output_chunk` | `s` | Every model call that streams |

Each measurement carries the tags `gen_ai.operation.name`, `gen_ai.request.model` and `gen_ai.response.model`, and
the same backend as the call's `chat` span in `gen_ai.provider.name`, `server.address` and `server.port`. A token
usage also carries `gen_ai.token.type`, which is `input` or `output`.

The names are those of the Microsoft.Extensions.AI version Typeshift references. The conventions they follow are
still in development, so a later version may rename an instrument or a tag.

## Message content

Spans leave out messages and the arguments and results of tool calls unless the environment variable
`OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT` is `true`:

```bash
export OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true
```

The variable is read once per process, so set it before your app starts. The `chat` spans then carry the messages of
their call, and each `execute_tool` span carries its call's arguments in `gen_ai.tool.call.arguments` and its result
in `gen_ai.tool.call.result`. The events of a [decision](#decisions) follow the same switch, under the same two
names: an asked decision then carries its call's arguments, which for questions are the questions, and a settled
call that your app's answer stands in for carries what the model reads in the tool's place, such as a rejection's
reason or the user's answers.

An exception's message is no message content. The status of a failed `execute_tool` or `invoke_agent` span carries
it whatever the variable says, and so does the entry that
[ends a failed run](./conversations.md#what-is-stored) in the stored conversation. So a trace backend and the
store receive what a database driver or an HTTP client wrote into its exception.

::: warning Captured content is your customers' data
With the variable set, your traces hold every run's messages and every tool call's arguments and results, including
what your customers wrote and what your tools read for them. Turn it on only where your tracing backend may keep that
data.
:::

## Related

- [How a run works](./how-a-run-works.md): the steps the spans of a run cover.
- [Wrapping a client](./models.md#wrapping-a-client): why `UseOpenTelemetry()` on your client records each call twice.
- [Conversations](./conversations.md#live-updates): live updates of a run, for a UI.
- [Native AOT](./native-aot.md): publishing your app ahead of time.
