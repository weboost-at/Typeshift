---
name: extend-settings
description: Change how Agentic tool settings and agent settings work - storable member types, configurable chat options, the five settings layers, the generated Settings and Overrides records, or their JSON. Use when a configurable member type is rejected with AGT014 or when settings resolve differently than expected.
---

# Extend settings

Read the "Settings layering" and "Persistence" sections of `docs/contributing/architecture.md` and decisions D25-D28
first. Settings are a contract stored in hosts' databases: a change to their JSON is a breaking change for stored
data, and stored documents carry no version (see the known gaps in `docs/contributing/status.md`).

## How a value flows

1. The toolset marks members `Configurable(s => s.X)`; the agent marks chat options the same way. The generator reads
   the picks (D5) and `Parsing/Storage.cs` decides per member which `StorableJson` write and read methods store it,
   or reports AGT014.
2. `Emitting/ToolsetEmitter.cs` writes `{Tool}Overrides`; `Emitting/AgentEmitter.cs` writes the agent's `Settings`;
   `Emitting/StorableEmitter.cs` writes the `Serialize`/`Deserialize` members both share.
3. At runtime `AgentDefinition.CreateChatOptions` builds `ToolSettingsLayers` per tool and the bindings
   (`Tools/*ToolBinding.cs`) apply them in order: code defaults, stored tool defaults (skipped when the agent's code
   sets the tool's settings), the agent's code settings, its stored settings, the run.

## Supporting a new member type

1. `src/Agentic/Persistence/StorableJson.cs`: a `Write` overload and a `Read...` method. A null value is left out; a
   missing or mistyped value reads as null and never throws.
2. `src/Agentic.Generators/Parsing/Storage.cs`: map the type (and its nullable form) to the two methods.
3. `tests/Agentic.Tests/Persistence/Support/EveryKind.cs` and `StorableTestData.cs`: add a member of the type;
   `StorableJsonTests` then round-trips it.
4. Update the type list in the AGT014 message (`Descriptors.cs`), in `docs/guide/diagnostics.md` (AGT014) and in
   `docs/guide/tool-settings.md`.
5. Accept the snapshots only if the generated code changed as intended (skill `change-generated-code`).

## Changing the layers

The layer order is user-facing (`docs/guide/tool-settings.md`, behaviors 25-29 in `docs/contributing/status.md`).
`SettingsLayeringTests` holds one test per rule; change the rule's test first, then the code, then the docs table.
