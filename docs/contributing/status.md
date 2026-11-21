---
title: Status
description: "Where the library stands: the slices of the rewrite, the test classes of each area, and the known gaps."
---

# Status

What is built, which test classes cover each area, and what is still open. Open items live here, never as TODOs
in code.

## Slices

The rewrite built the API the owner approved in ten slices, one at a time, and all ten are done:

1. Toolsets and tools: generated functions, overrides, tool lists, the analyzer.
2. Agents, registration, `AgentHost` runs.
3. Settings store, ids in code.
4. Conversations: checkpoints, resume, live updates, steering.
5. Approval, rules, plan and auto modes.
6. Middleware, context providers.
7. Telemetry, compaction.
8. Sub-agents, handoff.
9. MCP, built-in toolsets.
10. Native AOT: the console sample, the package consumer.

## Behavior coverage

Each test class covers one subject, and each test's name states the rule it checks, so the classes of an area are
where you find the test of a behavior. A new test class goes into the row of its area: the docs rules fail on a class
that no row names, and on a name that is no test class.

| Area | Test classes |
|---|---|
| The runtime, its hosts and its catalog | `TypeshiftRuntimeTests`, `RuntimeCatalogTests` |
| Registration: the chain, its stores, the interceptor | `AddTypeshiftTests`, `StoreRegistrationTests` |
| Providers and chat clients, the default rule, the call site's provider and model | `ChatClientResolutionTests`, `HostSelectionTests` |
| Tools: schemas, arguments, overrides, unions | `ToolSchemaTests`, `ToolInvocationTests`, `ToolOverridesTests`, `UnionToolTests` |
| Tool results and failed calls | `ToolResultWireTests`, `ToolFailureTests` |
| Runs: options, instructions, own tools, usage, the raw path, the caller's context | `AgentRunTests`, `ChatOptionsBuilderTests`, `ResolvedChatOptionsTests`, `OwnToolsTests`, `ChatResponsesTests`, `SynchronizationContextTests` |
| Structured output | `StreamingResultTests`, `UnionResultTests` |
| Settings, their layers and ids | `StoredSettingsTests`, `AgentSettingsTests`, `IdScopeTests` |
| Stored JSON | `SettingsJsonTests`, `ConversationEntryJsonTests`, `DecisionDocumentJsonTests` |
| Stores and their contract | `StoreExtensionsTests`, `FileAgentSettingsStoreTests`, `DecisionStoreTests` |
| Conversations: storage, resume, steering, live updates | `ConversationRunTests`, `ConversationStoreTests`, `ConversationHistoryTests`, `PersistAllConversationsTests`, `ChatMessagesTests` |
| Approval, rules and modes | `ToolApprovalTests`, `ModeTests`, `LegacyApprovalTests` |
| Decisions: answers, expiry, recovery, standing approvals | `DecisionAnswerTests`, `DecisionLedgerTests`, `ExpiredDecisionTests`, `CancelledDecisionTests`, `DecisionRecoveryTests`, `DecisionConcurrencyTests`, `StandingApprovalTests`, `ToolLoopGuardTests` |
| Questions to the user | `QuestionTests` |
| Middleware and context providers | `RunMiddlewareTests`, `ToolMiddlewareTests`, `ContextProviderTests` |
| Compaction | `CompactionTests`, `SummaryTranscriptTests` |
| Sub-agents | `SubAgentTests`, `SubAgentResultTests`, `SubAgentUsageTests`, `OpenDecisionBackgroundTests` |
| Handoffs | `HandoffTests` |
| MCP servers | `McpTests` |
| Spans, metrics and logs | `TelemetryTests`, `DecisionTelemetryTests`, `LogEventTests` |
| What a run allocates | `AllocationBudgetTests` |
| The generator: toolsets, tool names, XML docs | `ToolsetGenerationTests`, `ToolNamesTests`, `XmlDocsTests` |
| The generator: agents, their hosts, modules | `AgentGenerationTests` |
| Diagnostics | `DiagnosticTests`, `DiagnosticCatalogTests`, `HalfTypedCodeTests` |
| Incremental generation and its cost | `IncrementalTests`, `IncrementalCostTests`, `DiscoveryTests`, `InterceptParserTests`, `EquatableArrayTests` |
| The repository's rules | `TestConventionTests`, `SourceConventionTests`, `PublicApiSurfaceTests`, `PublicApiDocumentationTests`, `DecisionCitationTests`, `RunTimeErrorTests`, `LogEventCatalogTests`, `ModelShapeTests`, `ReservedToolNameTests`, `SnapshotFileTests`, `BuildFileTests` |
| The code of the guide | `samples/Typeshift.Samples.Docs`, whose scenarios CI runs on a canned client |
| Native AOT, trimming and the package | `samples/Typeshift.Samples.Console`, published both ways and run by CI; `samples/Typeshift.Samples.PackageConsumer`, run by CI after the pack |

## Known gaps

- **Every edit parses every toolset and agent.** The outputs stay cached after an edit of an unrelated file, but the
  generator binds the constructors of each toolset and agent and parses them again (D3). Measured with
  `dotnet run --project tools/Typeshift.Tools.Generators -c Release -- measure`, the least CPU time of 15 runs on a
  shared machine of 4 cores and the median bytes; a pair is a toolset of three tools and an agent that uses two
  toolsets:

  | Pairs | CPU per run | Allocated per run | Per pair |
  |---|---|---|---|
  | 0 | 0.5 ms | 81 KB | |
  | 5 | 16 ms | 1.5 MB | 296 KB |
  | 10 | 29 ms | 2.7 MB | 273 KB |
  | 50 | 126 ms | 12.7 MB | 255 KB |
  | 100 | 166 ms | 25.3 MB | 253 KB |

  A toolset of ten tools that share one settings type costs about 765 KB per pair. Binding the constructors is 70%
  of a pair's cost; binding only what each chain names would cut into it, and waits for projects that need it.
- **A run's limit across sub-agents.** `WithMaxToolIterations` bounds the model calls of one agent's run. Each run
  of a sub-agent counts its own, and one response may call a sub-agent many times, so an agent limited to three
  rounds made four model calls and its sub-agents 615 in a measurement. Nothing bounds the model calls, the tokens or
  the sub-agents working at once of a whole run. A budget the run's tree shares, which `AgentRun` carries and each
  sub-agent's run takes on from its caller, is a design of its own next to D32 and D33.
- **No run timeout.** A run ends when its model stops calling tools, it fails or its token is cancelled. No run option
  ends it after a time, and a run that waits for a background sub-agent waits with the run's token only, so a tool
  that never returns holds the run and its claim on the conversation. Your app cancels the token, for example with a
  `CancellationTokenSource` built on a `TimeProvider`.
- **Sub-agents cannot ask.** A call of a sub-agent that would wait for your app is rejected, and a sub-agent is not
  offered `ask_user` (D33, D82). Passing its decision up to the caller's conversation would pause the calling run
  inside a tool call.
- **No feed of decisions for watchers.** A watcher receives a run's decisions with the run's last update, as the
  caller does. An answer recorded from another request or process reaches no watcher, so a page that shows a
  chat's prompts in two places loads them with `GetDecisionsAsync`.
- **A run going on keeps its mode and its standing approvals.** `SetModeAsync` and `RevokeStandingApprovalAsync`
  change the conversation's decisions, which a run reads when it begins. A run going on, in this container or
  another, ends in the mode and with the approvals it began with; cancelling the run's token stops it at once.
- **The last answer during a run.** `AnswerAsync` records and then resumes. While a run of the conversation goes
  on in the container, the answers are recorded and the resume throws as any second run does, so the app answers
  again or calls `ResumeAsync` once that run ended.
- **A stopped run and the calls it had not begun.** A batch counts as started from the moment a run begins to
  carry it out (D80), so after a run stopped, the approved calls it had not reached are not run either: the model
  reads that their effect is unknown and asks again if it still wants them.
- **Compaction's estimate.** About four characters make a token, corrected by the provider's report of the run's
  last call against the estimate sent with it, or of the conversation's last call that stored both. A conversation's
  first call trusts the estimate alone, and so does the first call after runs without a maximum, or after entries
  stored before a model response kept its estimate. The summary model reads results cut to 2,000 characters. A
  result a run cleared among the turns its summary kept is stored cleared with that summary, so a later run's
  summary reads the placeholder in its place.
- **No output guardrail of its own.** Run middleware cannot change an answer a model gave: `run.Response` is
  read-only once `next` was called (D30). What a model says is checked or changed by a `DelegatingChatClient`
  around the provider's client, which the guide shows. A guardrail the chain names, applied below the checkpoints
  so that it also sees a streamed answer update by update, is not built.
- **Messages run middleware replaces, under compaction.** Compaction sees the message middleware put in place of
  another, never the stored one. Among the summarized turns it is summarized as it reads, so the stored summary
  follows the replacement. Among the turns a summary keeps it is left out of the stored summary, as every message the
  conversation never stores is, so later runs see neither the message your app sent nor its replacement.
- **Uses inside a referenced project.** TSH007 follows the handoffs and sub-agents of your project's agents into a
  referenced project's agent, but not that agent's own uses, which no stamp lists. An own tool of yours and one of an
  agent the referenced agent hands off to are not compared, in either project.
- **Steering in a stream.** `StreamingRun.Response`, typed or not, is built from the whole stream. When the provider
  sets no `MessageId` on its updates, two answers in one run (steering input, a background sub-agent's answer) merge
  into one message, and `Result` reads the first of them, where `RunAsync<T>` reads the last.
- **Unions in settings.** A configurable member cannot be a union (TSH012), since the generator writes no stored
  form for one.
- **The file store's save on the caller's thread.** Until its first wait, a save creates the folder and opens the
  lock file on the thread that called it, so a desktop app's UI thread waits for the disk. D85 leaves the caller's
  context without a hop to the thread pool; a hop for the file store alone would move this work off that thread.
- **What TSH032 does not compare.** The analyzer compares its reading of a declaration with the generated code by
  the stamps, which hold the tools and an agent's name, kind and whether it is described (D2). A schema version, a
  provider type, a chat option or the text of a description that names another generator's output is in no stamp,
  so the generator's fallback goes unreported. An agent that another generator writes as a whole class has no
  generated part and no stamp, so nothing reports it at build time and its host is missing at run time.
- **The empty union as a default.** A union parameter that defaults to `default` states `"default": null` in its
  schema, which none of its cases accepts, although the tool binds JSON null to that default.
- **Trackers of value-type ids under Native AOT.** The container closes the tracker's open generic over reference
  types only there, so an app registers the tracker of each value type it injects with `WithConversationTracker<TId>()`.
  A generator that read the injected trackers could register them itself.
- **A provider type nothing serves.** No diagnostic sees an agent whose provider id type no registered
  `IChatClientProvider<T>` serves, since the registration may lie in another project. Its runs take a client keyed
  by the provider and fail without one, naming the `IChatClientProvider<T>` they need (D63), so the mistake shows
  at the first run, not at build time.
- **An id type no store keeps.** As for provider types, no diagnostic sees a `WithSettingsId`, a
  `WithConversationId` or a `ConversationTracker<TId>` whose id type none of the registered stores keeps, since the
  registration may lie in another project. Without stores every id type works in memory, so the first call with
  such an id fails only once a store is registered, naming the store interface it needs. A check at startup of the
  id types the call sites name would need the generator to collect them across projects.
- **No fallback, no model catalog.** A failed model call ends the run: Typeshift moves to no other model or
  provider, and `RetryAsync` continues the run on what its settings name. It keeps no list of a provider's models,
  so a model a settings page stores is checked for being a name at all (D28) and no further.
- **No effective settings for a settings page.** `LoadSettingsAsync` returns what is stored, so a form shows empty
  fields where the agent's code or a record's default applies, and `ResolveOptionsAsync` returns the chat options
  without the tools' settings. A generated `ResolveSettingsAsync()` that fills every member of `Settings` from the
  layers below a run would close it; it needs the provider in its typed form and each tool's overrides made from
  its resolved settings, which no generated member writes yet.
- **Obsolete and experimental types in the collected docs.** The class the package adds to a project makes
  Weboost.SchemaAnnotations name every documented type of the project in a generated file (D55). A documented type
  marked `[Obsolete]` is CS0618 there, an error where warnings are errors, and one marked `[Experimental]` an error
  in any build. `TypeshiftSchemaAnnotations=false` or removing the type's docs is the way out today. The fix is a
  version of Weboost.SchemaAnnotations whose generated file suppresses both. Typeshift's own generated files suppress
  both already, so the package consumer sample holds an obsolete toolset, without docs until this repository
  references that version. The same collection compiles the docs of every documented type into the
  assembly, where only the types that tools and typed runs reach are needed.
- **A project below C# 14 that only documents types.** The generator writes nothing below C# 14 (TSH031), also not
  the module of a project without toolsets and agents, which gets no diagnostic. The XML docs of its types then
  describe nothing in the schemas of the tools that take them, and a JSON context it holds is not registered.
- **One conversation through unrelated hosts.** An app that runs one conversation id through the hosts of two agents
  that never hand off to each other shares their standing approvals and the conversation's mode, and no diagnostic
  sees it.
- **One container per conversation.** A run claims its conversation in its container only (D50): an app's service
  provider, or a runtime from `TypeshiftRuntime.Create`, so two runtimes in one process are two containers too.
  Across containers, the store refuses an entry under a number its conversation holds, and so stops the run that
  appends second, after that run may have called the model or a tool. A resume calls the model before it appends,
  so with several instances an entry without an ending one still looks the same for an interrupted run and for one
  another instance is on. Steering (`TrySteer`) and live updates (`WatchAsync`, `Runs`) reach the runs of their own container
  only. A claim in the store, such as a lease with a heartbeat, and a publisher behind the tracker would carry both
  across containers, and wait for a user who needs them.
- **Hosted MCP servers a client cannot send.** A `HostedMcpServerTool` (D35) reaches the provider only through a
  client of an API with hosted MCP. Other clients, such as OpenAI's Chat Completions client and Anthropic's default
  adapter, drop it without an error, and Typeshift neither logs nor throws. The guide warns about it; a check
  against the client that serves the run would need a decision next to D35.

## Related

- [Decisions](./decisions.md): why the built behavior is shaped the way it is.
- [Architecture](./architecture.md): where each behavior lives in the code.
