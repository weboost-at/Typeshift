# Demos

Three apps that use the library as a host would, each runnable offline on canned models and against a real provider
with one argument. They are not part of the package, the solution or CI: `dotnet build demos/Typeshift.Demos.slnx`
builds them.

| Demo | Shows |
|---|---|
| [MultiProvider](./Typeshift.Demos.MultiProvider/README.md) | Users define model providers at runtime and pick a model per user; every `CLUNKY n` comment marks where the current library gets in the way. |
| [Coder](./Typeshift.Demos.Coder/README.md) | A long-running coding agent: plan mode, approval and rules, the built-in toolsets, a background explorer, compaction, a conversation on disk that resumes, steering, the live list of runs. |
| [Support](./Typeshift.Demos.Support/README.md) | A support desk of four agents: handoffs, a sub-agent, an MCP server, a context provider, middleware, settings per customer, typed output. |

`Typeshift.Demos.Shared` holds what every host writes: the provider catalog and routing (`providers.json`), a JSON
file store, a scripted model, and a console listener for the spans.
