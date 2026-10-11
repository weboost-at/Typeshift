# Status

The alpha of Agentic, built in one autonomous run. This file is the plan and the memory of that run: milestones are
ticked as they land, open items and blockers are recorded here, never as TODOs in code.

## Plan

### Milestone 1: skeleton

- [ ] Repository infrastructure: `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`,
      `.gitignore`, `LICENSE`, `README.md`, `CHANGELOG.md`, `AGENTS.md`, `CLAUDE.md`, `Agentic.slnx`, local tools
- [ ] Empty projects wired together: runtime, generator, the three test projects, the sample
- [ ] CI running build, tests and slopwatch
- [ ] Convention tests from the test-formatting skill, test bases and the test-data catalog

### Milestone 2: tools (behaviors 1-9)

- [ ] Runtime: `Toolset`, the tool builder, `ToolName`, the settings accessor, the generated-function base, JSON
      options through DI
- [ ] Generator: toolset parsing and emitting, toolset diagnostics (AGT001-AGT005, AGT009, AGT011, AGT013)
- [ ] Tests: toolset snapshot, one test per diagnostic, behaviors 1-9

### Milestone 3: registration (behaviors 10, 30-35)

- [ ] Assembly modules and stamps, the `AddAgentic()` interceptor
- [ ] Tool and agent catalogs, cross-assembly registration
- [ ] AGT008, AGT014
- [ ] Tests: module and interceptor snapshots, cross-assembly stamps, `InternalsVisibleTo`, behaviors 10, 30-35

### Milestone 4: agents and settings (behaviors 11-29, 36-37)

- [ ] `AgentDefinition`, the typed configuration facades, the settings layering, `AgentRuntime<TAgent>`
- [ ] Store interfaces with in-memory implementations
- [ ] Agent diagnostics (AGT006, AGT007, AGT010, AGT012)
- [ ] Tests: agent snapshot, behaviors 11-29, 36-37

### Milestone 5: stored agents (behaviors 38-40)

- [ ] The stored-agent record, the export from code agents, catalog integration

### Milestone 6: conversations (behaviors 41-48)

- [ ] The port of the conversation tracking behind `IConversationStore`, on `TimeProvider`

### Milestone 7: AOT, packaging, docs (behavior 49)

- [ ] The sample, the AOT CI job, the packaging smoke test
- [ ] `docs/architecture.md`, `docs/diagnostics.md`, `docs/decisions.md`, `docs/renaming.md`, README
- [ ] Final review pass, clean re-verification, pull request, tag `v0.1.0-alpha.1`
