# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [SemVer](https://semver.org/).

## [Unreleased]

The first alpha. The API will change.

### Added

- Toolsets: static methods become `AIFunction`s through a source generator, with schemas from their parameters and
  XML docs, reflection-free argument binding, and a DI scope per call.
- Tool settings: settings records passed to the tool as a parameter the model never sees, with configurable members
  marked in the tool's chain (`WithSettings<T>().Configurable(s => s.Limit)`, or
  `WithConfigurableSettings<T>().NotConfigurable(s => s.Timeout)`) and five layers (code defaults, stored
  tool defaults, agent code settings, agent stored settings, run settings).
- Agents: `AgentDefinition` with chat options set through `WithChatOptions().WithModel(...).WithTemperature(...)`,
  instructions from an overridable `GetInstructions()`, tools included or excluded from a generated list
  (`UseToolset<T>().Include(t => t.FindCompanies)`) and tool settings through a generated builder per tool
  (`ConfigureFindCompanies().WithLimit(3)`); misuse of either is a compiler error.
- Typed settings: a generated `Settings` record per agent and `{Tool}Overrides` record per tool, holding only what
  may change at runtime, including the model; serialized without reflection.
- `AgentRuntime<TAgent>` with generated typed members: `ResolveChatOptions`, `ResolveChatOptionsAsync`,
  `LoadSettingsAsync`, `SaveSettingsAsync`, `ConfigureAsync` and `CreateChatClientAsync`.
- Persistence through one store, `ITypeshiftStore<TId>`, of self-serializing documents under an id type the host
  chooses, registered with `AddTypeshiftStore<TStore, TId>()`; `InMemoryTypeshiftStore<TId>`. Without a store the
  persistent APIs throw and agents run from code.
- Conversation tracking: every model exchange stored as a session of a conversation, streaming included, when
  `ChatOptions.ConversationId` parses as the store's id type.
- `services.AddTypeshift()`, intercepted per project, registering the toolsets and agents of the project and its
  references, and the JSON contexts that list their tools' parameter and result types.
- Diagnostics TSH001 to TSH014; TSH009 warns about a tool that does not take its settings.
- Native AOT support, proven by the console sample.
