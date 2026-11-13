# Demos

Three apps that use the library as a host would, each runnable offline on canned models and against a real provider
with one argument. They are not part of the package or the solution: `dotnet build demos/Typeshift.Demos.slnx`
builds them. No pull request waits for them. The `demos` workflow builds them and runs each one offline on `main`
and once a day.

| Demo | Shows |
|---|---|
| [MultiProvider](./Typeshift.Demos.MultiProvider/README.md) | Users define model providers at runtime and pick a model per user; every `CLUNKY n` comment marks where the current library gets in the way. |
| [Coder](./Typeshift.Demos.Coder/README.md) | A long-running coding agent: plan mode, approval and rules, file, shell and web toolsets of its own, a background explorer, a conversation on disk that resumes, steering, the live list of runs. It compacts only a run longer than the offline one. |
| [Support](./Typeshift.Demos.Support/README.md) | A support desk of four agents: handoffs, a sub-agent, an MCP server, a context provider, middleware, settings per customer, typed output. |

`Typeshift.Demos.Shared` holds what every host writes: the provider catalog and routing (`providers.json`), a JSON
file store, a scripted model, and a console listener for the spans.

The demos keep conversations and settings under `.typeshift/` in the current directory. Delete that folder after
updating to a Typeshift version whose store format changed, such as 0.1.0-alpha.5: the demos read older files as
empty conversations.
