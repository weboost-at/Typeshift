---
title: Audit
description: "The October 2026 audit of the runtime, the generator and the built-in toolsets: what was checked, what was fixed, what stays open, and how each finding was verified."
---

# Audit

An audit of the whole library before v1, on 2026-10-16: the runtime, the generator and the analyzer, the built-in
toolsets and the samples, read line by line and probed with tests and small experiments. Nine findings were fixed
on the branch `audit-2026-10`, each with a test where one can run on Linux. The rest are recorded as known gaps in
[Status](./status.md#known-gaps) and as proposals in the [retrospective](./retrospective.md).

## Scope and method

- Every file of `src/Typeshift` and `src/Typeshift.Generators`, the samples and the test bases.
- The parts of Microsoft.Extensions.AI the library leans on, decompiled where the docs left a question: the tool
  loop's limits and its approval handling, `ChatOptions.Clone`, the OpenTelemetry client's lifetime, and the OpenAI
  client's use of `ChatOptions.ModelId`.
- A probe test per suspected bug, written to fail first, then the fix. An experiment against the OpenTelemetry SDK
  for the metrics finding.
- Three demo hosts under `demos/` that use every feature offline on canned models; what they showed feeds the
  retrospective.

The threat model: the host authorizes conversation and settings ids (D42) and the store is trusted infrastructure.
The model is not trusted, and neither are tool results, MCP servers or fetched pages.

## Fixed

| # | Severity | Where | Finding |
|---|---|---|---|
| A1 | High | `Telemetry`, `AgentHost`, `Compaction` | The metrics reset after every run: a new `OpenTelemetryChatClient`, with a meter of its own, was created and disposed per run. |
| A2 | Medium | `ConversationChatClient`, `ConversationRun` | Steering input waiting at a handoff was stored as the handing agent's, so the conversation's next run went back to it. |
| A3 | Medium | `ApprovalChatClient` | An approval lost its stored request when run middleware changed the count of messages ahead of the input. |
| A4 | Medium, Windows only | `FileSystemRoot` | A last path segment ending in dots or spaces passed the root check and reached the root's parent. |
| A5 | Low | `ChatResponses` | Merging the rounds of a run added the later rounds' usage into the first response's object. |
| A6 | Low | `LivePublisher` | Merged live updates lost the ids of the updates they merged. |
| A7 | Low | `ConversationRun` | A background sub-agent cancelled by its run's end faulted an unobserved task. |
| A8 | Low | `ConversationChatClient` | Options the raw path built itself ran with default run options above the tool loop and the agent's inside it, and an agent with handoffs loaded its conversation twice per run. |
| A9 | Low | `ModuleEmitter` | An empty assembly name crashed the generator. |

### A1. The metrics reset after every run

`Telemetry.Instrument` wrapped the provider's client in a new `OpenTelemetryChatClient` for each run and disposed
it with the run's pipeline. The wrapper creates an `ActivitySource` and a `Meter` per instance, and the
OpenTelemetry SDK ends the instruments of a disposed meter. An experiment with the SDK's `MeterProvider` showed
`gen_ai.client.token.usage` at a count of 1 after each of three runs, where one long-lived meter counts 1, 2 and 3.
Every dashboard built on the token and duration histograms was wrong, and each run paid for a source, a meter and
four histograms.

The fix, `InstrumentedClients`, keeps one wrapper per provider client instance in a `ConditionalWeakTable`,
created the first time a run uses that client and kept as long as the client lives; the run wraps it in
`UndisposedChatClient`. The provider's metadata, its name, address and default model, still reaches every span,
because the wrapper reads it from the client it wraps. The test
`Given_Two_Runs_On_One_Provider_Client_Their_Chat_Spans_Come_From_One_Source` holds it. A provider that returns a
new client per call now keeps a meter per client, so the contract asks for one instance per model.

### A2. Steering input waiting at a handoff

`HandOffAsync` stored the handoff's result for the agent handed to, then closed the run, which stored the steering
input still waiting as the handing agent's. The conversation's last entry then named the wrong agent, its next run
went back to that agent, and when the target hands back, the handoff bounced at once. `CloseAsync` now takes the
agent that answers next. Test: `Given_Steering_Input_Waiting_At_A_Handoff_The_Agent_Handed_To_Answers_It`.

### A3. Approvals under run middleware

`Prepare` read the stored requests from the first `InputStart` messages of the call. Run middleware may add or
remove messages ahead of the input, as the guide says, which shifted the history past that count: the answer found
no request, was dropped, and the call never ran, without a word. The requests are now read from the whole call,
which is safe because the conversation layer drops an input's own requests before anything stores them (D24). Test:
`Given_Middleware_That_Prepends_A_Message_An_Approval_Still_Runs_The_Stored_Call`.

### A4. Windows path trimming

Windows drops trailing dots and spaces from a path's last segment when it opens the path, and `Resolve` compared
the path before that happened: `list_files(".. ")` was not `..`, passed the `Within` check as the root plus a
segment of two dots and a space, and listed the root's parent. The followed path now goes through `Path.GetFullPath` before the check, which trims on Windows
and changes nothing on Linux, where CI runs, so this fix has no test.

### A5 to A9

- A5: `Merge` set the merged usage to the first response's object and added into it, so middleware that kept a
  pass's response saw its usage grow with later passes. The sum is now an object of its own.
- A6: `LivePublisher` built a merged update without the `MessageId` and `ResponseId` of the first update, so a
  watcher's `ToChatResponse()` grouped the text into messages differently from the run's caller. The merged update
  is a clone of the first.
- A7: `StartBackground` discarded a task that faulted when the run's end cancelled the sub-agent. `AnswerAsync`
  returns null in that case, and the task completes.
- A8: `ResolvedChatOptions.From` gave options the raw path built itself default run options, while the tool loop
  below ran with the agent's. Both are the agent's now. The conversation of an agent with handoffs was loaded once
  to find the answering agent and again for the history; it is loaded once.
- A9: `ModuleEmitter.GetName` indexed the first character of an empty name.

## Open

Findings that need a design decision are known gaps in [Status](./status.md#known-gaps), and the ones that change
the API are proposals in the [retrospective](./retrospective.md). The ones worth a line here:

- **A required argument sent as null** binds as `default`: null for an `int` parameter is 0, for a `string`
  parameter it is null, and the tool fails inside its own code with a message the model cannot act on. The generated
  binding knows which parameters are nullable, so a null for one that is not should fail like a missing argument.
  Low, in `GeneratedToolFunction.GetArgument`.
- **`ConfigureAsync` retries without a cap** against a store that always conflicts. Low, D41.
- **The raw path's client under concurrent calls** shares one tool loop whose iteration limit the approval layer
  sets per call. Low, D32.
- **MCP tools are listed on every run**, a round trip per run unless the host's provider caches them. Performance.
- **A conversation loads whole** on every run, and three scans run over every entry: the history, the standing
  approvals and the lost background agents. Performance; the retrospective proposes the store change.
- **The token estimate** measures every tool's schema text again before each model call. Performance, minor.
- **Sub-agents in plan mode** are hidden, read-only ones included. Design; the retrospective proposes the mark.

## Checked and sound

What the audit looked for and did not find, so the next one need not start from zero:

- **Approval.** An input cannot bring a request of its own, since requests are dropped before storage. An answer
  runs the stored call whatever call it carries, an answer to no stored request is dropped, a request keeps its
  first answer, and a rejection wins over every approval. `exit_plan_mode` only moves to `Default` or `Auto`. The
  tests under `Approval/` cover each rule.
- **Sub-agents** inherit the caller's mode and standing approvals, cannot ask the host, and stop when the run
  that started them ends.
- **The file tools** refuse absolute paths, `..` and links that lead outside, following every link on a path;
  `search_files` skips reparse points, and patterns run without backtracking or with a timeout. A link swapped in
  between the check and the use still escapes, as documented.
- **The shell** runs behind approval, with a timeout that kills the process tree and caps on both streams.
- **`web_fetch`** takes absolute http and https URLs only. Refusing private addresses is the fetcher's job, and the
  coder demo's `SafeWebFetch` shows the `ConnectCallback` the guide asks for.
- **Storage.** Documents and entries serialize without reflection on generated paths, a newer version is refused,
  revisions guard every save, a store that fails to append is logged and traced and never fails the run, and
  storing never observes the run's token.
- **Concurrency.** One run per conversation per process, bounded feeds that never block a run, steps tracked by
  reference, and a steering queue, a background counter and a closing flag that stay consistent under every order
  tried.
- **Lifetimes.** Hosts are transient over the scope that resolved them; providers, rules, middleware and roots are
  scoped; agents, toolsets, the store and the instrumentation are singletons; nothing static is mutable.
- **Native AOT.** The console sample publishes and runs, and generated paths reflect over nothing.
- **The generator.** Models hold no symbols or locations, discovery is narrowed by syntax, every shape the parser
  rejects is a diagnostic, and the generated code never holds a compiler error. The empty assembly name was the
  one crash found.

## Related

- [Status](./status.md): the known gaps, the audit's included.
- [Retrospective](./retrospective.md): what building on the library showed, and what to change before v1.
- [Decisions](./decisions.md): D36, which the telemetry fix changed, and the decisions the findings cite.
