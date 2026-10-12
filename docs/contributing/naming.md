# Naming

The library was called `Agentic` until its first release, but `Agentic` on nuget.org belongs to another author. The
owner named it **Typeshift** (D38). [Renaming](./renaming.md) lists every place the name goes.

## Why Typeshift

- It says what the generator does: plain C# toolsets and agents shift into typed, reflection-free code at compile
  time, with a typed settings record per agent and per tool.
- It reads well in every place the name goes: the package ID, `using Typeshift;`, `services.AddTypeshift()`,
  `services.AddTypeshiftStore<TStore, TId>()` and the diagnostic prefix `TSH`.
- It was free on nuget.org as an exact ID on 2026-10-12, checked against
  `https://api.nuget.org/v3-flatcontainer/typeshift/index.json`; reserving the `Typeshift.*` prefix keeps later
  packages under the name.
- It is short, one word, and spelled the way it sounds.

Against it: an iOS word game is also called Typeshift, and npm has an unrelated, unmaintained `typeshift` package.
Neither is a .NET library.

## What lost

| Name | Against |
|---|---|
| `Agentic` | Taken on nuget.org. |
| `Weboost.Agentic` | Free, but shares the namespace and the search results with the other `Agentic`. |
| `Agentype` | Ambiguous to say ("a-gen-type"); taken on PyPI, and its GitHub organization is taken. |
| `Agentric` | One letter from `Agentic`: users would install the wrong package. |
| `Agentwright`, `Toolwright` | Long; "wright" is rare outside compounds. |
| `Caddie`, `Squire`, `Precast`, `Famulus` | Fit the idea, but say nothing about types or compile time. |
| `Cogent`, `Operant`, `Agentry`, `Mandate` | A common word, a psychology term, a misreading of `Agentic`, a policy library. |
