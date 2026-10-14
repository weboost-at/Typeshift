---
name: extend-settings
description: Change how Typeshift tool settings work - configurable members, the generated Overrides records and how they apply. Use when a settings member is rejected or when settings resolve differently than expected.
---

# Extend settings

- A tool's settings record is the user's; the generator reads its settable public properties
  (`SymbolExtensions.GetPublicProperties`) and writes `{Tool}Overrides` with the configurable ones, nullable.
- Configurable members are an allow list unless `WithConfigurableSettings` makes them a deny list (D18). Obsolete
  members stay out unless picked.
- `{Tool}Overrides.ApplyTo(settings)` replaces each member a layer sets and keeps the others; nested records are
  replaced whole (D19).
- What a configurable member's type may be is `Parsing/Storage.cs` (D19): it yields a `StorageModel` per type, which
  `Emitting/StorageEmitter.cs` turns into the member's JSON, through `Generated/StorableJson.cs` for scalars and
  generated `WriteStored{n}`/`TryReadStored{n}` helpers for the rest. A new storable type touches all three, and
  `EveryKindSettings` in `tests/Typeshift.Tests/Persistence/Support`.
- A rejected member: TSH012 (no stored form) and TSH013 (no setter) come from `ToolsetParser.ParseSettings`.
- The agent's `Settings` record stores the chat and run options `Emitting/OptionMembers.cs` lists, and the tools'
  overrides under `"tools"` (D44).

Tests: `ToolOverridesTests` and `Persistence/` (runtime), the snapshots of `ToolsetGenerationTests` and
`AgentGenerationTests` (generator).
