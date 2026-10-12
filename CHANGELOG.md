# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [SemVer](https://semver.org/).

## [Unreleased]

The first alpha. The API will change.

### Added

- Toolsets: static methods become `AIFunction`s through a source generator, with schemas from their parameters and
  XML docs, reflection-free argument binding, and a DI scope per call.
- Tool settings: settings records read through `GetToolSettings<T>()`, with configurable members picked in code and
  five layers (code defaults, stored tool defaults, agent code settings, agent stored settings, run settings).
- Agents: `AgentDefinition` with chat options, tools selected through generated methods
  (`UseToolset<T>(tools => tools.FindCompanies())`) and tool settings through generated `WithToolSettings`
  overloads; misuse of either is a compiler error.
- Typed settings: a generated `Settings` record per agent and `{Tool}Overrides` record per tool, holding only what
  may change at runtime, including the model; serialized without reflection.
- `AgentRuntime<TAgent>` with generated typed members: `ResolveChatOptions`, `ResolveChatOptionsAsync`,
  `LoadSettingsAsync`, `SaveSettingsAsync`, `ConfigureAsync` and `CreateChatClientAsync`.
- Persistence through one store, `IAgenticStore<TId>`, of self-serializing documents under an id type the host
  chooses, registered with `AddAgenticStore<TStore, TId>()`; `InMemoryAgenticStore<TId>`. Without a store the
  persistent APIs throw and agents run from code.
- Conversation tracking: every model exchange stored as a session of a conversation, streaming included, when
  `ChatOptions.ConversationId` parses as the store's id type.
- `services.AddAgentic()`, intercepted per project, registering the toolsets and agents of the project and its
  references.
- Diagnostics AGT001 to AGT014.
- Native AOT support, proven by the console sample.
