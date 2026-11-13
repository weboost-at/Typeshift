# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [SemVer](https://semver.org/).

## [Unreleased]

### Added

- `store.VerifyAsync(settingsId, otherSettingsId, conversationId, otherConversationId)` checks a store whose settings
  ids and conversation ids have different types, such as settings per tenant under a `string` and conversations
  under a `Guid`, in one call. A call with the ids of one type checks only the interfaces of that id type.

### Changed

- TSH020 is titled "Referenced assembly was built with another Typeshift version or without the generator", and its
  message names both causes. It read as if the reference had never run the generator, also when it was built with
  another Typeshift version.
- **Breaking:** `store.VerifyAsync(id)` is now `store.VerifyAsync(id, otherId)`. The check stores a document and an
  entry under the second id first and fails when they changed, or when they load under the first id, so a store that
  forgets the id in a query no longer passes. Such a store served one customer's settings or conversation to another.
  Pass two ids nothing is stored under; the same id twice throws an `ArgumentException`.
- `VerifyAsync` also fails a store that orders a conversation by its entries' times, since the entries it appends
  now carry times that tie and run backwards, and a store that writes a document which is not stored when a save
  names a version other than 0, where a `StoreConflictException` belongs. Agents' own settings are checked under
  kinds named after the id, from `typeshift_check_<id>` on, instead of new kinds on every call.
- **Breaking:** a store registered in the container with `AddScoped` or `AddTransient` fails the first run with an
  `InvalidOperationException` that names it: register it with `WithStore<T>()` or `AddSingleton`, and let it take a
  factory such as `IDbContextFactory<T>`. Typeshift takes its stores from the root container. Before, such a run
  failed with the container's own message where scopes are validated, and elsewhere one instance of the store, with
  the `DbContext` it took, served every request.
- Stored JSON keeps text outside ASCII as it is written, in settings, conversation entries and the file store's
  files. Before, every such character was stored as `\uXXXX`, so Chinese, Japanese and Korean text took about twice
  the bytes, a row could not be read in a database tool, and a `LIKE` search found nothing. Quotes, `<`, `>` and `&`
  are still escaped, and JSON stored by earlier versions reads as before. Compaction now counts a tool result that
  is a JSON string by its text, so its estimate no longer grows when a conversation is loaded from the store.
- A tool result that compaction cleared asks the model to call the tool again only when the tool is marked
  `ReadOnly()`. Every other cleared result now reads `[Cleared to save context.]`, so the model is not invited to
  send, pay or delete a second time. Mark the tools that only read.
- A compaction summary that fails no longer ends the run. When the summary named a model or a provider of its own,
  the run's own provider and model write it instead. When that fails too, the run's call goes out without a summary,
  with the oldest tool results cleared, and the provider decides. Each failed summary call is logged as a warning and
  recorded on the run's span. Before, a summary model that was down, or one the run's provider did not know after a
  user switched providers, failed every later run of the conversation before its own model was called.
- TSH003's message says why the method cannot be a tool, as in "'Lookups.LookUp' cannot be a tool: its class
  'Lookups' is file-local, and generated code is written to other files".
- TSH004 is titled "Tool parameter or result cannot be bound". It covers a tool's result too, and its message names
  the reason, as in "The result of the tool 'chars' cannot be returned to the model: its type is a ref struct".
- A tool named `get_type`, `memberwise_clone` or `reference_equals` is `GetTypeTool`, `MemberwiseCloneTool` or
  `ReferenceEqualsTool` in an agent's `Settings`, as on the tool list. Without the suffix the member hid one that
  every record has from `object`, which is CS0108. Settings stored under the tool's name are not affected.
- The module of an assembly whose name holds other characters than letters, digits and dots carries a hash of the
  name, as `Acme_Tools_1a2b3c4dModule` for `Acme-Tools`. The generated registration names it for you.
- Module stamps are at version 7, so a referenced project built with an earlier Typeshift is TSH020 until it is
  rebuilt.

### Fixed

- A streaming run's spans form one tree: its `execute_tool` spans and its `chat` spans after the first update are
  children of the run's `invoke_agent` span, as in a run that does not stream. They attached to the span of the
  caller, such as the web request's, or started traces of their own.
- With `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT` set, every run's `execute_tool` spans carry the call's
  arguments and result, in `gen_ai.tool.call.arguments` and `gen_ai.tool.call.result`. A run that does not stream
  left them out.
- A `chat` span and its metrics name the backend that answered the call. With several clients under one provider
  id, such as a client per customer, every span carried the provider name and server address of the client the app
  used first.
- An app whose model names or provider ids come from data, such as a model per customer, no longer keeps a meter and
  its instruments for each of them as long as it runs. Typeshift instruments each backend once, whatever provider
  ids and models the runs name.
- A project on another framework than .NET 11 fails at restore with NU1202, which names `net11.0`. The restore
  passed before, and the build then failed with CS0246 on `Agent` and `Toolset` and with CS0400 in a generated file,
  none of which named the framework.
- The package hid CS1591, the warning for undocumented public members, in every project that turned XML docs on in
  its project file. It hides it only in a project that leaves `GenerateDocumentationFile` unset, as the docs said.
- In a project that leaves `GenerateDocumentationFile` unset, the compiler's warnings about XML comments, such as
  CS1573 for a missing `<param>`, no longer fail a build that treats warnings as errors. They show as warnings.
- The XML file of a project that leaves `GenerateDocumentationFile` unset stays in `obj`. It was copied to the output
  folder, published with the app and packed with the project.
- A `UseToolset<T>()` whose type does not resolve yet, such as `UseToolset<>()` while you type, no longer stops the
  generator: it failed with CS8785 and every generated member of the project disappeared until the name resolved.
- Tool names no longer depend on the culture of the machine that builds. Under Danish, Norwegian and Faroese
  cultures (`da`, `nb`, `nn`, `no`, `fo`), a method whose name ends in an upper-case `A` before `Async`, such as
  `EnableMFAAsync`, kept the suffix and became `enable_mfa_async`, while every other machine built `enable_mfa`. A
  project built only under one of those cultures now gets the name without the suffix. Stored settings and standing
  approvals are kept under the tool's name, so pin the old one with `WithName("enable_mfa_async")` or let it change.
- A negative `WithSchemaVersion` compiles under cultures that write another minus sign, such as Swedish, Norwegian
  and Finnish.
- Two toolsets or agents whose full names differ only by case, such as `TicketTools` and `Tickettools` in one
  namespace, no longer stop the generator with CS8785.
- A type that does not resolve yet, as while you type its name, is left to the compiler. Generated code no longer
  names it, so its error is not repeated in a generated file, and TSH005, TSH012, TSH015, TSH025, TSH028 and TSH030
  are no longer reported beside it, some of them about a type named `''`.
- A model response with a content of a type Microsoft.Extensions.AI's JSON contract does not know, such as one a
  provider's client adds, is stored without that content, and a warning names the type. Before, the whole step was
  missing from the conversation. A stored content that the installed version of Microsoft.Extensions.AI cannot read
  is left out when its entry loads, where the whole conversation failed to load, and the next run logs how many
  contents it went without.
- A stored enum value without a name reads back: a `[Flags]` value with no flag set where the enum has no member for
  zero, a combination with a bit no member names, a number cast to the enum, or such a value as a dictionary's key.
  It is stored as its number. Before, it was saved without an error and read back as missing, so the layer below
  applied instead, and settings saved that way take effect once you upgrade.
- The file store of `WithFileStore()` fails a load with an `InvalidDataException` that names the file and its shape
  for every file of another shape, such as `[]`, a `version` in quotes or a `kind` that is a number. Those threw
  System.Text.Json's `InvalidOperationException` without the file's name. A save that cannot take its lock file
  within 30 seconds throws a `TimeoutException` that names the lock file, where it waited for ever without a word,
  and a save removes the unfinished files that saves of its kind left when their process died.
- A store of agents' own settings whose creation failed once, such as one whose database was not up at the first
  run, is created again on the next run. Before, every later run of every agent failed with that first exception
  until the app restarted.
- Compaction keeps a conversation under `MaxInputTokens` in the runs after one that cleared tool results. A run
  corrected its first estimate against the stored history, which keeps the full results, so its first call could
  exceed the maximum up to four times, and a provider with a smaller context window refused every later run. Each
  model response of a run with a maximum now stores what its call was estimated at, in the new optional
  `ConversationEntry.EstimatedInputTokens`, and the next run corrects its estimate by that. Entries stored before
  load as they are, and a run after them starts with the plain estimate.
- The summary model reads what the tools were called with and what they returned. It read a .NET type name in place
  of each call's arguments, and the placeholder of a cleared result in place of almost every result. It now reads
  each call's arguments and the first 2,000 characters of each result, as text where the result is text.
- Compaction leaves alone what a context provider added to a run, and what run middleware added to the messages it
  received. Such a message among the summarized turns went to the summary model, could come back in the stored
  summary, and was missing from the run's later calls. It now stays in every call of the run and counts toward the
  target, and no summary reads or stores it.
- Clearing skips a tool result that is no longer than the note that replaces it. Clearing such a result made the call
  longer and took a value, such as an id or a count, that the model was still using.
- A toolset, an agent, a tool method and a type a tool takes or returns may be marked `[Obsolete]` or
  `[Experimental]`. The generated files named them without a `#pragma`, so CS0612, CS0618 or the experimental id was
  reported once more in files your own `#pragma` does not reach. That failed a build that treats warnings as errors
  and, for `[Experimental]`, every build.
- A tool parameter of a reference type that defaults to null without a `?`, as `string query = null!` or as
  `string query = null` in a project without nullable annotations, no longer gives CS8625 in the generated file. Its
  schema allows null, as that of a `string?` parameter does.
- A `file` class is reported where generated code would have to name it: as a toolset or an agent, or nested in one,
  by TSH016, as the class of a tool's method by TSH003, and as a settings record or its type argument by TSH006. Each
  compiled into errors such as CS0115 and CS0234 inside generated files, and two file-local toolsets of one name
  stopped the generator with CS8785.
- TSH004 reports the tool signatures that generated code cannot compile: a `dynamic` parameter, a parameter of an
  array of pointers, a result that is a pointer, a ref struct or `dynamic`, and a task or a stream that may itself
  be null, such as `Task<string>?`. Each ended in errors or warnings inside a generated file, such as CS1962 and
  CS8602.
- A static member of a C# 14 extension block can be a tool, as in `AddTool(string.Shout)`. The generated code
  called it by a name that does not compile. A tool method of a class closed over a nullable type, such as
  `Finder<string?>.Find`, no longer gives a nullability warning in the generated file.
- A `CancellationToken?` parameter receives the call's token. It was offered to the model as an argument.
- Four rare declarations no longer end in errors or warnings inside generated files. A tool name that ends in a
  line break, as `WithName("echo\n")`, is TSH008. A tool whose settings record has no settable member gets no
  `Configure{Tool}()`, which was a builder with nothing to set and warned with CS9113. A control character in a
  description stays out of the generated doc comments. A default declared by `[DateTimeConstant]` compiles.
- A reference under an `extern alias` alone is reported: TSH019 on a `UseToolset<T>()` of one of its toolsets, and
  TSH020 for the reference, whose toolsets and agents `AddTypeshift()` cannot register. Generated code named its
  types from `global::`, which gave CS0400 in the generated agent and CS0234 in the generated registration.
- Names that met a generated one no longer end in errors inside generated files:
  - Two tools of one toolset that would share a generated member, such as `create_report` and `report_function`,
    are TSH007, which names the member. They gave CS0102.
  - A class named like a member the generator writes into it, such as an agent named `Settings` or a toolset named
    `EchoFunction` with a tool `echo`, is TSH018. It gave CS0542.
  - Two referenced assemblies whose names differ only in punctuation, such as `Acme.Tools` and `Acme-Tools`, no
    longer share one module name, which gave CS0433.
  - A settings member named like a parameter or a local of the generated code, such as `settings`, `writer` or
    `value`, compiles. It gave CS0019 and CS0165.
- A toolset with a tool that returns `IAsyncEnumerable<T>` builds in a project whose packages bring System.Linq.Async
  before 7.0. The generated code named `System.Linq.AsyncEnumerable`, which exists twice there, and failed with
  CS0433.
- A cancelled call stops reading a stream tool at its next item, also when the tool's method takes no
  `CancellationToken`. Such a stream was read to its end before the cancellation showed.

## [0.1.0-alpha.6] - 2026-11-12

### Added

- `TypeshiftRuntime.Create(client)` runs the agents of an app without a container of its own, with one chat client
  for every run: `await using var research = runtime.Agent<ResearchAgent>()`, then `research.RunAsync("Vienna")`.
  `AddTypeshift()` registers the runtime as a singleton too, so a background service injects it and takes a host for
  each job. Each host the runtime gives you has scoped services of its own, which disposing the host disposes. The
  package now depends on `Microsoft.Extensions.DependencyInjection`.
- The docs site publishes the guide as Markdown for coding assistants: `llms.txt` lists its pages, `llms-full.txt`
  holds them all in one file, and each guide page is also served at its address with `.md`.
- Tools take and return C# 15 unions, and `RunAsync<T>` returns one. A JSON string binds a union's `string` case and
  a number its number case, and an object binds the case whose properties it carries, so a union of records needs no
  `[JsonUnion]` classifier.
- `T`'s XML docs describe the schema `RunAsync<T>` asks the model to answer in, as they describe the types tools take.
- `RunStreamingAsync<T>` streams a typed run: read its updates to the end, and its `Response` holds the answer typed,
  as `RunAsync<T>` returns it.
- `RunAsync<T>` and `RunStreamingAsync<T>` take a `ChatMessage`, so the run that answers an approval asks for `T`
  again.
- `UseAgent<T>().WithResult<TResult>()` holds a sub-agent's answer to the JSON of `TResult`, which the calling model
  reads as the call's result, or as its answer in the background. An answer without it fails the call, and the
  calling model reads why.
- An agent without `WithDescription` takes its description from the `<summary>` of its XML docs, as a tool takes
  its description from its method's. That description satisfies TSH023, names the agent's sub-agent tool and handoff,
  and is the `gen_ai.agent.description` of its spans. Without `GenerateDocumentationFile`, TSH011 says the summary is
  lost.
- A run takes an `IChatClient` registered under its provider, such as with `services.AddKeyedChatClient("openai",
  client)`, or the one registered for every agent with `services.AddChatClient(client)`, when no
  `IChatClientProvider<T>` of its agent's provider type is registered. An agent that names no model runs on that last
  client, with its own model, so a demo needs neither a provider class nor `WithModel`.
- `WithInstructions("...")` sets an agent's instructions in its constructor; override `GetInstructions()` only for
  instructions your code builds per run.
- Each agent has settings of its own, which every run applies under no settings id: `ConfigureAsync(configure)`,
  `LoadSettingsAsync()` and `SaveSettingsAsync(settings, loadedVersion)`, kept by an `IAgentSettingsStore`.
  `InMemoryTypeshiftStore<TId>` implements it, and a settings id's settings apply over the agent's own.
- `WithFileStore()` keeps each agent's own settings in a JSON file, for an app without a database: in the `settings`
  folder of a `typeshift` folder beside your app's assemblies, unless `store => store.Folder = folder` names another.
  `WithStore(store)` registers a store you created.
- `host.WithSettingsId(id)` and `host.WithConversationId(id)` give a run its ids in code, and each id's type picks the
  store of that type, so settings under `int` ids and conversations under `string` ids serve one app. `WithStore<T>()`
  takes stores of any id type, and TSH028 reports only a type that is no store.
- `AddTypeshift().WithOptions(options => options.PersistAllConversations = true)` keeps the runs that pass no
  conversation id too: each run with input starts a conversation under a new id, a `Guid` of version 7, or under an id
  of `options.NewConversationIds(...)` for a conversation store of another id type. A raw-path call's new id is a
  string, the `Guid`'s text unless `NewConversationIds` makes strings, so the response's `ConversationId` continues it
  in `ChatOptions.ConversationId`.
- `WithConversationTracker<TId>()` registers the `ConversationTracker<TId>` of one id type as a singleton of its own,
  which an app published with Native AOT needs for a value type such as `Guid`.

### Changed

- `AgentHost<TAgent>` is `IAsyncDisposable` and `IDisposable`. Disposing a host your container resolved, such as one
  injected into a request, does nothing, so code that injects hosts works as before.
- A run that ends while background sub-agents work, for example cancelled or failed, waits until the sub-agents it
  stops have stopped, so none works on after the run.
- Live updates are on without `WithRealtimeTracking()`, so `ConversationTracker<TId>.WatchAsync` reads a conversation's
  updates from the start, where it threw. `WithRealtimeTracking(options)` only tunes them now.
- The response of a run with a conversation names it in `ChatResponse.ConversationId`, and so does each update of a
  streaming run, where it was null.
- An answer to an approval request that is no longer open throws `ApprovalClosedException` before the run stores
  anything or calls the model, where the run dropped the answer and called the model with no new input. Its
  `Requests` say why each request is closed: answered already, passed by, or unknown. Catch it to tell your user that
  the approval has expired, as the chat endpoint sample does with 409 Conflict.
- A run that takes steering input after it asked for approval closes its requests once it answers the input, as a
  new message does, and leaves them out of its `ApprovalRequests`. An answer to one ran the call, although the model
  had gone on without it. A background agent's answer closes none.
- The package targets .NET 11 instead of .NET 10, so your app needs .NET 11 and its SDK.
- `Result` of a typed run whose answer lacks the `data` property throws a `JsonException`, where it threw an
  `InvalidOperationException`. `TryGetResult` returns false, as before.
- `IConversationStore.LoadConversationSinceCompactionAsync` has no default any more, so your store implements it. A store
  that cannot start at the last summary returns `(await LoadConversationAsync(id, ct)).SinceLastSummary()`.
- The class of an agent's host members is `TypeshiftHost_4Demo12SupportAgent` for `Demo.SupportAgent`, in the global
  namespace and hidden from type completion, where it was `SupportAgentHostExtensions` in the agent's namespace. Code
  that called the members through the class's name, or with `using static`, calls them on the host instead.
- The package turns on `GenerateDocumentationFile` unless your project sets it, and hides CS1591 then, so tools get their
  descriptions with no `Directory.Build.props`.
- Without any store, settings and conversations are kept in memory, and the first save logs a warning, where every
  load and save threw. Once you register a store, with the chain or in the container yourself, an interface none of
  your stores implements still throws.
- The container has a `ConversationTracker<TId>` for every id type you inject, where it had the tracker of the build's
  id type only. Under Native AOT, register the tracker of a value type with `WithConversationTracker<TId>()`.
- The raw path's `ChatOptions.ConversationId` names a conversation under a `string` id, in your
  `IConversationStore<string>`, where its text was parsed into the build's id type. For a conversation under an id of
  another type, resolve the options on `host.WithConversationId(id)`.
- Each agent's providers are of the type of the provider its `WithModel(provider, model)` names, where
  `TypeshiftProviderIdType` typed every provider of the build: `WithModel(Provider.OpenAi, "gpt-5-mini")` makes the
  agent's `Settings.Provider` and `SummaryProvider` a `Provider?`, and an agent without `WithModel` stores strings.
  One app may run an agent of enum providers beside agents of strings, since each run asks the
  `IChatClientProvider<T>` of its agent's type. `WithChatClientProvider<T>()` registers the class as each
  `IChatClientProvider<T>` it implements, and a client keyed by a provider of any type serves a run without one.
- TSH029, still "Provider id type cannot be used", reports a provider whose type the agent's settings cannot store as
  one value, on the provider in `WithModel`, and only when the agent's settings store it. An agent whose model stays
  in code may name a provider of any type.
- TSH030 is "Type is no chat client provider" and reports a `WithChatClientProvider<T>()` whose class implements no
  `IChatClientProvider<T>`, where it reported a provider of another type than the build's.
- TSH006 no longer reports a public agent of a less accessible settings or conversation id type. It still reports one
  whose configurable model names a less accessible provider type, and any agent whose configurable model names a
  provider type nested as `private` or `protected` or declared `file`, which the generated `Settings` cannot name.
- An agent whose model is configurable but that calls no `WithModel` stores its providers as strings, where they were
  of `TypeshiftProviderIdType`. A stored enum provider reads back as its name and reaches your
  `IChatClientProvider<string>`, and a stored number reads back as null, so the layer below decides. To keep the old
  type, name a default provider of it with `WithModel(provider, model)`, or serve strings.
- `WithModel(provider, model)` throws an `ArgumentNullException` on a null provider, where the agent ran as if it
  named no provider.
- Module stamps are at version 6, so a referenced project built with an earlier Typeshift is TSH020 until it is
  rebuilt.

### Removed

- `TypeshiftIdType`, the MSBuild property that typed every settings and conversation id of a build. Delete it from
  your `Directory.Build.props`: a host's ids take their types from `WithSettingsId` and `WithConversationId`.
- The `global using` aliases `ITypeshiftStore`, `ISettingsStore`, `IConversationStore`, `InMemoryTypeshiftStore`,
  `ConversationTracker` and `LiveRun`. Name the id type instead, as in `ITypeshiftStore<Guid>`,
  `InMemoryTypeshiftStore<int>` or `ConversationTracker<string>`.
- `TypeshiftProviderIdType`, the MSBuild property that typed every provider of a build. Delete it from your
  `Directory.Build.props`: each agent's providers take their type from its `WithModel`, so an agent that names
  `WithModel(Provider.OpenAi, "gpt-5-mini")` has providers of your `Provider` enum.
- The `IChatClientProvider` alias, the last alias the generator wrote. Name the provider type your agents'
  `WithModel` names: `class Clients : IChatClientProvider<string>`, or `IChatClientProvider<Provider>` for an enum.
- TSH021, which reported a referenced project with agents built with another provider id type. Agents of other
  provider types work beside yours.
- The `settingsId` and `conversationId` parameters of the generated `RunAsync`, `RunAsync<T>`, `RunStreamingAsync` and
  `RunStreamingAsync<T>`, and the `settingsId` parameter of `ResolveOptionsAsync`. Give the ids to the host:
  `support.RunAsync(input, settingsId: 42, conversationId: chat)` is
  `support.WithSettingsId(42).WithConversationId(chat).RunAsync(input)`, and `settings` may now go second without its
  name.
- The generated overloads that took an id first: `ResumeAsync(conversationId, ...)`,
  `ResumeStreamingAsync(conversationId, ...)`, `LoadSettingsAsync(settingsId)`,
  `SaveSettingsAsync(settingsId, settings, loadedVersion)` and `ConfigureAsync(settingsId, configure)`. Call the member
  without the id on a host that has it, such as `support.WithConversationId(chat).ResumeAsync()` or
  `support.WithSettingsId(42).ConfigureAsync(configure)`. A `LoadSettingsAsync(default)` left over loads the agent's
  own settings.
- `AddCoreServices<TId, TProviderId>`, which only generated code calls, is `AddCoreServices`, without type
  parameters.

### Fixed

- TSH028 and TSH030 no longer report a type parameter, such as that of a helper generic over a store or a chat client
  provider whose constraints make it one.
- `RunAsync<T>` on a conversation that a handoff passed on, or one that hands off itself, asked the agent that
  answers for no schema. That agent answers in `T` now.
- An answer to an approval request that a later run of the conversation passed by ran the call, and approving it for
  the conversation gave the tool a standing approval. Only the requests the last run left without an answer can be
  answered now; an answer to any other runs nothing and stores nothing.
- A run that loaded a conversation while the conversation's last run was still ending could miss that run's last
  entries, such as its approval requests, and drop the answers to them. A run now claims its conversation before it
  loads it: a second run throws, as it does while a run goes on, and one that starts while another still loads the
  conversation waits until that run has begun.
- A streaming run stored its approval requests only after the host had read them, so a host that stopped reading at
  a request left it unstored, and the answer to it ran nothing. The run now stores the requests first.
- A provider that numbers its calls per response, such as a local server with an OpenAI-compatible API, made the run
  that answered a later approval request fail with a duplicate key, because the conversation held the call's id
  already, and could pair a call left without a result with a later call's result. A call whose id the conversation
  holds, in its history or not, is now renamed, as `call_1_2`, before anything stores or runs it.
- A run that started while a handoff passed the conversation to the other agent could take the conversation first, so
  the handed-off run failed. A handoff now passes its claim on the conversation to the agent handed to, and so does a
  run passed on to the agent that answers the conversation; a run that starts meanwhile waits, then throws as it does
  while any run goes on.
- Live updates of a run that does not stream carried every approval request the run stored, those a rule, a standing
  approval or auto mode answered included, and an answer to one of those now throws. They carry only the requests
  left to your app.
- Each case in the schema of a union or of a polymorphic type names its type, which Anthropic's client needs. It
  rejected a tool or a typed run with such a type, and a typed run of an object union reached it with no schema at all.
  `RunAsync<T>` asks for a union, a polymorphic type or a dictionary under a `data` property now, as
  Microsoft.Extensions.AI does for a number, since Anthropic's client sent no schema for them.
- The typed members of an agent's host, such as `RunAsync`, are in scope in every file. They lived in the agent's
  namespace, so a file that named the agent in full, without importing its namespace, found no `RunAsync` on
  `AgentHost<TAgent>`.
- A tool parameter or a `RunAsync<T>` type with a member System.Text.Json cannot handle, such as an `Exception`, sent
  the provider a schema it rejected, with a message about a missing type. It now fails before the model is called,
  naming the member.

## [0.1.0-alpha.5] - 2026-10-30

### Added

- `ISettingsStore` and `IConversationStore` split the store in two. A store implements either or both, and
  `WithStore<T>()` registers each one it implements, so settings and conversations can live in different databases.
  `ITypeshiftStore` is both in one, so an existing store compiles unchanged. A call that loads or saves without a
  matching store throws, naming the interface it needs, and TSH028 reports a store that implements neither interface
  for the build's id type.
- An agent may declare tools of its own with `AddTool(method)` in its constructor, backed by a static method of the
  agent that may be private. The call returns the same chain as a toolset's tool. The agent offers these tools after
  its toolsets' tools: each gets a `Configure{Tool}()`, its configurable members join the agent's `Settings`, it can
  require approval, and the analyzer checks it like a toolset's tool. Its name may repeat another agent's tool, unless
  the two share standing approvals through a handoff or a sub-agent (TSH007). Module stamps carry each agent's tools,
  at stamp version 4.
- `store.VerifyAsync(id)` runs the rules Typeshift relies on against your store, for every interface it implements,
  from a test of your own with any test framework, and throws an exception that names the rule your store broke.

### Changed

- A document's schema version travels in its JSON as `"$schemaVersion"`, left out at 1, so a store keeps the JSON and
  its version and nothing else. `SaveAsync` and `StoredDocument` carry no schema version, and
  `ConversationEntry.Deserialize` and the generated `Settings.Deserialize` take the JSON alone. A store moves over by
  dropping its schema version columns and the schema versions it passed to `StoredDocument` and
  `ConversationEntry.Deserialize`. Documents and entries stored at schema version 1 read as before. For an agent that
  raised its schema version, copy the stored one into the JSON as `"$schemaVersion"` if your migrations read it;
  without it a document reads as schema version 1.
- The number a store keeps for each document is its version, and the record's number its schema version.
  `StoredDocument.Revision` and `Stored<T>.Revision` are `Version`, and `SaveAsync` and `SaveSettingsAsync` take
  `loadedVersion` where they took `revision`, so `StoredDocument.Version` now holds what `Revision` held.
  `WithSettingsVersion(n)` is `WithSchemaVersion(n)`, and `IStorable.Version`, `Settings.Version` and
  `ConversationEntry.Version` are `SchemaVersion`. A store renames its revision column, or keeps it and maps it.
- The package icon and the docs site's touch icon are a white tile with an ink and a red chevron, where the tile was
  ink with a white and a red chevron.

### Removed

- The built-in toolsets: `TodoToolset`, `FileSystemToolset`, `ShellToolset` and `WebToolset`, with `IFileSystemRoot`,
  `IWebSearch`, `IWebFetch`, `WithFileSystemRoot<T>()`, `WithWebSearch<T>()` and `WithWebFetch<T>()`. An app that used
  them declares the tools it needs in a toolset of its own. Compaction still keeps the latest call of a tool named
  `write_todos`.

## [0.1.0-alpha.4] - 2026-10-28

### Added

- `System.Text.Json` and Newtonsoft.Json serialize a `ConversationEntry` as it is, with no settings: the first writes
  its stored JSON, the second that JSON as a string. Newtonsoft.Json used to read entries back with their contents
  emptied.

### Changed

- `ITypeshiftStore.LoadAsync` and `SaveAsync` are no longer generic. They take a document's id and kind, a save its
  JSON and version too, and a load returns a `StoredDocument`. Typeshift serializes the agents' settings itself, so a
  store handles strings and numbers only. A store moves over by taking `kind`, `json` and `version` where it called
  `TData.Kind`, `Serialize()` and `TData.Version`, and returning `new StoredDocument(json, version, revision)`; the
  stored data stays the same.

## [0.1.0-alpha.3] - 2026-10-27

### Added

- `TypeshiftProviderIdType` sets the type of provider ids for the build, `string` by default, and the generator writes
  the `IChatClientProvider` alias for it. A string, a number, a `Guid` or an enum works.
- TSH030 reports a provider of another type than the build's at `WithModel` and at `WithChatClientProvider<T>()`.
- `ITypeshiftStore.LoadConversationSinceCompactionAsync` returns a conversation's last summary and the entries after it.
  Its default loads the whole conversation; a store overrides it to leave the entries before the summary unread.
- A tool may return `IAsyncEnumerable<T>`: the call reads it to its end, and the model receives its items as one array.
- With `WithRealtimeTracking()`, a run that does not stream publishes too: each model response and tool result as it
  is stored, where only streaming runs reached `WatchAsync`.
- A configurable member may be a record struct, stored like a record. Other structs stay TSH012.

### Changed

- An agent names its model by a provider and a model: `WithModel(provider, model)`. `GetChatClientAsync` receives
  both, and a request carries the model alone as `ChatOptions.ModelId`, so composite names and the client wrappers
  that undid them go. A configurable model brings `Provider` into the agent's `Settings`, a configurable summary model
  `SummaryProvider`.
- Compaction always summarizes: without a summary model the run's model does it, and `WithSummaryModel(model)` names
  a model the run's provider serves. It no longer drops the oldest turns.
- TSH029 is "Id type cannot be used" and covers `TypeshiftProviderIdType` too.
- Plan mode offers an agent's sub-agents. A sub-agent called in plan mode sees only its own read-only tools, no
  `exit_plan_mode`, and answers its caller, which presents the plan.
- TSH021 covers `TypeshiftProviderIdType` too, so a referenced project with agents built with another provider id
  type fails the build instead of its runs. Module stamps carry the provider id type, at stamp version 2.
- A run loads its conversation from the last summary on. A summary keeps the conversation's mode, its standing
  approvals and the background calls still working; a run of a conversation an earlier version summarized loads it
  whole.

### Fixed

- The metrics `gen_ai.client.token.usage` and `gen_ai.client.operation.duration` no longer reset after every run:
  each provider and model is instrumented once and kept as long as the host, instead of a source and a meter per
  run, and each call goes on to the client its run got, so a provider may return a new client per run or per
  tenant.
- Steering input waiting when the model hands the conversation to another agent is stored for that agent, so the
  conversation's next run goes to it instead of back to the agent that handed off.
- An approval still finds its stored request when run middleware adds or removes messages ahead of the input.
- TSH007 reports a tool name two referenced projects share, on the project that references both.
- Merging the rounds of a run no longer adds the later rounds' usage into the first response's `Usage`.
- A tool parameter of a recursive type gets `$ref`s that resolve: they point into the parameter's schema instead of
  the function's root.
- The merged text updates `ConversationTracker.WatchAsync` publishes keep the first update's ids, so a watcher
  groups them into messages as the run's caller does.
- A background sub-agent that its run's end cancelled no longer faults an unobserved task.
- A public agent with a configurable model and a less accessible provider id type is TSH006 on the pick, instead of
  CS0053 in the generated `Settings`.
- The file tools compare a path as the operating system opens it, so on Windows a last segment's trailing dots and
  spaces cannot lead above the root.
- TSH023 and TSH024 check a sub-agent or handoff target of a referenced project too: its module stamp carries the
  agent's name, whether it has a description and the tools it offers that require approval, at stamp version 3.
- Options the raw path builds itself run with the agent's run options through the whole pipeline.
- The client `CreateChatClientAsync` returns serves concurrent calls, each with its own options' iteration limit,
  where a second call changed the first one's limit.
- A run of an agent with handoffs loads its conversation once instead of twice.
- The XML docs of a project that declares no toolsets or agents describe the tool parameters of the projects that
  reference it: such a project gets a module that registers them, where its docs reached no schema.
- TSH022 reports two agents of one kind in two referenced projects too, on the project that references both.
- `run_command` keeps what a command wrote just before its timeout: it reads the output to its end once the timeout
  killed the command, and for a second past the timeout while a process left behind holds it open, where it stopped
  reading at once.
- `ConfigureAsync` gives up after ten conflicts in a row and throws the last `StoreConflictException`, where a store
  that conflicted every time kept it going until its token was cancelled.
- A streaming run that compacted keeps its summary when steering input or a background answer starts another pass,
  where the pass lost it and could summarize the same turns again.
- A public agent of a less accessible `TypeshiftIdType` is TSH006 on the agent, instead of CS0051 in its generated
  host members, which the generator keeps internal.
- Compaction calibrates a run's first estimate by the tokens the provider reported for the conversation's last call,
  where only the run's own calls corrected it.

## [0.1.0-alpha.2] - 2026-10-16

### Added

- The properties of the types tools take are described by their XML docs: the package brings
  Weboost.SchemaAnnotations and adds the class it collects docs for to every project with
  `GenerateDocumentationFile`. `TypeshiftSchemaAnnotations=false` turns it off. A project that marked its own
  `[SchemaAnnotations]` class deletes it.
- `ConversationTracker.WatchRunsAsync(ct)` yields the runs going on, then each run as it starts and ends
  (`LiveRun.Ended`), so a list of running conversations needs no polling.

### Changed

- A failed tool call reaches the model with its exception's message, so the model can correct the call.

## [0.1.0-alpha.1] - 2026-10-16

The library was rebuilt from the API its owner approved; nothing of the unreleased first alpha shipped.

### Added

- Toolsets: static methods become `AIFunction`s through a source generator, with schemas from their parameters and
  XML docs and reflection-free argument binding. Tool names derive from the method (`CloseTicketAsync` is
  `close_ticket`); `WithName`, `WithDescription`, `ReadOnly()` and `RequiresApproval()` refine them.
- Tool settings: a settings record passed to the tool as a parameter the model never sees, with configurable members
  as an allow list (`WithSettings<T>().Configurable(s => s.Limit)`) or a deny list
  (`WithConfigurableSettings<T>().NotConfigurable(s => s.Timeout)`), and a generated `{Tool}Overrides` record.
- Agents: chat options, instructions, toolset uses with `Include` and `Exclude`, run options and
  `Configure{Tool}()`, read at compile time into a generated `Settings` record of what may change at runtime.
- `AddTypeshift()`: one intercepted call registers the toolsets and agents of the project and its references, an
  `AgentHost<T>` per agent and the assemblies' JSON contexts. Runs (`RunAsync`, `RunAsync<T>`, `RunStreamingAsync`,
  the raw `ResolveOptionsAsync` and `CreateChatClientAsync`) use the services of the host's scope and return
  Microsoft.Extensions.AI's types.
- Stored settings: `ITypeshiftStore<TId>` with revisions, `InMemoryTypeshiftStore`, `WithStore<T>()`, and per agent
  `LoadSettingsAsync`, `SaveSettingsAsync` and `ConfigureAsync`; runs take a settings id. The id type is the
  `TypeshiftIdType` MSBuild property; the generator writes `ITypeshiftStore` and `InMemoryTypeshiftStore` aliases for
  it. Settings serialize themselves member by member, `System.Text.Json` included, with kinds and versions.
- Conversations: runs with a conversation id resume it and store every step as it happens, so `ResumeAsync`
  continues a run a crash cut off. `ConversationTracker` lists the runs going on, steers them with `TrySteer`, and
  with `WithRealtimeTracking()` streams their updates to watchers.
- Tool approval: a run that calls a tool marked `RequiresApproval()` ends with Microsoft.Extensions.AI's approval
  requests, and the conversation's next run carries the answers; the stored request is the call that runs.
  `ApproveForConversation()` approves a tool for the rest of the conversation, and `IToolApprovalRule`s
  (`WithApprovalRule<T>()`) approve or reject calls before the host is asked.
- Plan and auto modes: `AgentMode` is a run option. Plan mode offers the read-only tools and `exit_plan_mode`, whose
  plan the host approves with `ApprovePlan(mode)`; auto mode runs what requires approval unless a rule rejects it. A
  conversation keeps its mode.
- Middleware and context: `IRunMiddleware` around every run, streaming or not, `IToolMiddleware` around every tool
  call (`WithRunMiddleware<T>()`, `WithToolMiddleware<T>()`), and `IContextProvider`s an agent names with
  `UseContext<T>()`, which add instructions, messages and tools to each run.
- Compaction: `WithMaxInputTokens`, `WithTargetInputTokens` and `WithSummaryModel` shrink a long run's history
  before each model call, clearing old tool results, then summarizing or dropping the oldest turns; the summary is
  stored, and later runs start from it.
- OpenTelemetry: the `Typeshift` source and meter trace each run as `invoke_agent`, with `chat` and `execute_tool`
  spans inside, after the GenAI semantic conventions.
- Multiple agents: `UseAgent<T>()` offers an agent as a tool that answers in a fresh context, in the foreground or
  `InBackground()`; `UseHandoff<T>()` hands the conversation to another agent, which answers its later runs. The
  analyzer checks both (TSH023, TSH024, TSH026).
- MCP: `UseMcpServer(name)` offers a server's tools, which the host's `IMcpToolProvider` connects
  (`WithMcpToolProvider<T>()`), with `RequiresApproval()` and `ReadOnly()`.
- Built-in toolsets in `Typeshift.Toolsets`: `TodoToolset` (`write_todos`), `FileSystemToolset` within an
  `IFileSystemRoot`, `ShellToolset` (`run_command`, behind approval) and `WebToolset` over the host's `IWebSearch`
  and `IWebFetch`.
- Native AOT: generated paths never reflect; the analyzer reports a tool type without JSON metadata in an AOT
  project (TSH027).
- An analyzer reports every Typeshift diagnostic at real source locations, so `#pragma` and `.editorconfig` reach
  them; a suppressor drops CS1573 for tool parameters the model never sees.
