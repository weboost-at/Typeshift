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
- The agent's `Settings` record stores the chat and run options `Naming/OptionMembers.cs` lists, and the tools'
  overrides under `"tools"` (D44).

Tests: `ToolOverridesTests` and `Persistence/` (runtime), the snapshots of `ToolsetGenerationTests` and
`AgentGenerationTests` (generator). A change of what the generator reads or writes follows the `change-generated-code`
playbook as well: its rules, the catalogs of shapes, the snapshots and the stamp version.

Docs: the storable types are listed in `docs/guide/tool-settings.md#storable-types`, the `Settings` JSON in
`docs/guide/settings-and-stores.md`.

## Finish

- **Changelog.** A line in `CHANGELOG.md` for what a user can observe, by the rule in `AGENTS.md`, "Commits".
- **Docs.** The pages the change checklist under "Docs" in `AGENTS.md` names for your change, the code they show
  from `samples/Typeshift.Samples.Docs`, through the `update-docs` playbook.
- **Demos.** After a change of public API, of generated code or of a diagnostic, build the demos as `AGENTS.md`,
  "Build and test", shows; fix a demo you broke, or say so under the **Demos** heading of the pull request.
- **Check.** The commands of `AGENTS.md`, "Build and test", with the suite green three times in a row.
