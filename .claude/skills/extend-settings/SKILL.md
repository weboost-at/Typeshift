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
- A rejected member: TSH013 (no setter) comes from `ToolsetParser.ParseSettings`.

Tests: `ToolOverridesTests` (runtime) and the snapshots of `ToolsetGenerationTests` (generator).
