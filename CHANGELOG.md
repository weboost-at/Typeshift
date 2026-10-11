# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [SemVer](https://semver.org/).

## [Unreleased]

## [0.1.0-alpha.1]

The first alpha. The API will change.

### Added

- Toolsets: static methods become `AIFunction`s through a source generator, with schemas from their parameters and
  XML docs, reflection-free argument binding, and a DI scope per call.
- Tool settings: settings records read through `GetToolSettings<T>()`, with an allowlist of configurable members and
  five layers (code defaults, stored tool defaults, agent code settings, agent stored overrides, run overrides).
- Agents: `AgentDefinition` with tool selection, chat options and tool settings in code; `AgentRuntime<TAgent>` with
  typed, allowlisted configuration views and a generated `ConfigureAsync`.
- Stored agents: agents defined as data, exported from code agents, run through the same path.
- Conversation tracking: every model exchange stored as a session of a conversation, streaming included, behind
  `IConversationStore`.
- Store interfaces with in-memory implementations, all optional.
- `services.AddAgentic()`, intercepted per project, registering the toolsets and agents of the project and its
  references.
- Diagnostics AGT001 to AGT015.
- Native AOT support, proven by the console sample.
