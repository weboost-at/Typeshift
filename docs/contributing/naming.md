# Naming

`Agentic` is taken on nuget.org (D38), so the library needs a name before its first release. This page holds the
shortlist and a prompt to brainstorm further with another agent. [Renaming](./renaming.md) lists every place the
chosen name goes.

## What the name must carry

- It is the package ID, the root namespace, the registration call (`services.AddAgentic()` becomes
  `services.Add{Name}()`), the store call (`AddAgenticStore`) and the diagnostic prefix (`AGT`), so it must read well
  in all of them.
- It is free on nuget.org as an exact ID, and ideally as a reserved prefix (`{Name}.*`) for later packages, such as
  store generators.
- It says something true about the library: agents and toolsets written as plain C#, turned into typed,
  reflection-free code at compile time, on Microsoft.Extensions.AI, ready for Native AOT.
- It is short, easy to spell after hearing it once, and not a common .NET type name (`Handler`, `Delegate`, `Relay`).

## Shortlist

Free on nuget.org as exact IDs on 2026-10-12, checked against `https://api.nuget.org/v3-flatcontainer/{id}/index.json`;
prefix reservations, trademarks and GitHub names are not checked.

| Name | Reads as | Why | Against |
|---|---|---|---|
| **Agentwright** | `services.AddAgentwright()` | A wright makes things by hand: agents crafted in code, the way a shipwright builds ships. | Long; "wright" is rare outside compounds. |
| **Cogent** | `services.AddCogent()` | Cog plus agent; "cogent" means clear and convincing, as typed settings and compile-time errors are. | A common word, harder to search; Cogent Communications exists. |
| **Toolwright** | `services.AddToolwright()` | Tools first: the generator's main product is the tool functions. | Undersells agents, settings and conversations. |
| **Operant** | `services.AddOperant()` | Something that operates and acts, as an agent does; rare and short. | Psychology term (operant conditioning). |
| **Agentry** | `services.AddAgentry()` | The craft or work of an agent; close to the old name, so the docs barely change in tone. | Easy to misread as "Agentic" or "Gentry". |
| **Tackle** | `services.AddTackle()` | A tackle box holds tools; to tackle a job. | Says little about AI. |
| **Rigging** | `services.AddRigging()` | Rigging an agent with its tools and settings. | A gerund as a package name reads oddly. |
| **Mandate** | `services.AddMandate()` | What an agent acts on: instructions, tools, limits. | Sounds like a policy or legal library. |

Also free, weaker: `Taskwright`, `Cogwright`, `Typesmith`, `Toolsmith`, `Wrangler`, `Agentum`, `Ferrule`, `Kitbash`,
`AgentLoom`, `AgentHarness`, `Gaffer`, `Bellhop`, `Errand`, `Mettle`, `Proxima`, `Actuator`.

Taken: `Foreman`, `Steward`, `Envoy`, `Emissary`, `Harness`, `Conduit`, `Loom`, `Quill`, `Courier`, `Concierge`,
`Factotum`, `Stagehand`, `Ensemble`, `Relay`, `Marshal`.

The recommendation is **Agentwright**: it is distinctive, searchable, free, describes the "agents written in code" idea, and
`AddAgentwright()` keeps the shape of today's API. **Cogent** is the shorter alternative if a common word is fine.

## Brainstorming prompt

Paste this into another agent session, with web access if it has it:

```text
I am naming a .NET 10 library and need a shortlist of 15 names, ranked.

What the library is:
- Developers write AI agents and their tools as plain C# classes: a Toolset declares tools from static methods,
  an AgentDefinition declares chat options, instructions, which tools it offers and their settings.
- A Roslyn source generator turns them, at compile time, into reflection-free Microsoft.Extensions.AI AIFunctions,
  a typed settings record per agent and per tool, and one DI registration call. It runs under Native AOT.
- Settings layer from code defaults to stored per-tenant values to per-run overrides; conversations are tracked and
  stored through one pluggable store.
- Audience: .NET developers building LLM features into existing apps, who value compile-time safety and plain code
  over frameworks and YAML.

Constraints:
- The name becomes the NuGet package ID, the root namespace, the registration call services.Add<Name>() and a
  diagnostic ID prefix of 3 letters. It must read well in all four.
- It must be free on nuget.org as an exact package ID. Check each candidate with
  https://api.nuget.org/v3-flatcontainer/<lowercased-name>/index.json (HTTP 404 means free) and drop taken ones.
- Avoid: names of popular .NET types or libraries (Handler, Relay, Conduit, Polly-likes), generic AI words alone
  (Agent, AI, GPT, LLM, Copilot, Brain), names of other AI products, and anything that only makes sense in English
  slang.
- One word preferred, at most 12 letters, easy to spell after hearing it once.
- Already considered: Agentic (taken), Agentwright, Cogent, Toolwright, Operant, Agentry, Tackle, Rigging, Mandate.
  You may rank these too, but bring at least 10 new ones.

For each name give: the name, services.Add<Name>(), the 3-letter diagnostic prefix, one sentence on why it fits,
one sentence against it, NuGet status as checked, and any obvious trademark or product clash you know of.
End with your top 3 and why.
```
