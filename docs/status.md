# Status

The alpha of Agentic, built in one autonomous run. This file is the plan and the memory of that run: milestones are
ticked as they land, open items and blockers are recorded here, never as TODOs in code.

## Plan

### Milestone 1: skeleton

- [x] Repository infrastructure: `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`,
      `.gitignore`, `LICENSE`, `README.md`, `CHANGELOG.md`, `AGENTS.md`, `CLAUDE.md`, `Agentic.slnx`, local tools
- [x] Empty projects wired together: runtime, generator, the three test projects, the sample
- [x] CI running build, tests and slopwatch
- [x] Convention tests from the test-formatting skill, test bases and the test-data catalog

### Milestone 2: tools (behaviors 1-9)

- [x] Runtime: `Toolset`, the tool builder, `ToolName`, the settings accessor, the generated-function base, JSON
      options through DI
- [x] Generator: toolset parsing and emitting, toolset diagnostics (AGT001-AGT005, AGT009, AGT011, AGT013)
- [x] Tests: toolset snapshot, one test per diagnostic, behaviors 1-9

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

## Behavior coverage

Every rule of `spec/behaviors.md` maps to one test or more; a behavior that bundles several rules has one test per
rule.

| # | Test |
|---|---|
| 1 | `ToolsetTests.Given_A_Tool_With_Settings_Its_Schema_Holds_Only_The_Model_Facing_Parameters` |
| 2 | `ToolsetTests.Given_No_Settings_The_Tool_Runs_With_Its_Code_Defaults` |
| 3 | `ToolsetTests.Given_Passed_Settings_Only_Their_Allowlisted_Members_Apply` |
| 4 | `ToolInvocationTests.Given_A_Scripted_Call_The_Tool_Receives_Its_Arguments`, `..._Given_Two_Scripted_Calls_Each_Runs_In_A_Scope_Of_Its_Own`, `..._Given_A_Scripted_Call_The_Tool_Runs_With_Its_Resolved_Settings`, `..._Given_A_Scripted_Call_The_Result_Reaches_The_Model_As_Json` |
| 5 | `ToolInvocationTests.Given_A_Missing_Required_Argument_The_Call_Fails_With_An_ArgumentException`, `..._Given_An_Absent_Optional_Argument_The_Tool_Takes_Its_Default` |
| 6 | `ToolInvocationTests.Given_A_Struct_Parameter_Defaulting_To_Default_The_Tool_Runs_Without_It`, `ToolSchemaTests.Given_A_Struct_Parameter_Defaulting_To_Default_Its_Schema_Carries_The_Default` |
| 7 | `ToolSchemaTests.Given_A_Nullable_Reference_Parameter_It_Is_Typed_As_Nullable` |
| 8 | `ToolSchemaTests.Given_The_Same_Method_The_Schema_Matches_AIFunctionFactory_Without_Descriptions` |
| 9 | `ToolSchemaTests.Given_Param_Docs_They_Become_The_Parameter_Descriptions`, `..._Given_No_WithDescription_The_Summary_Is_The_Tool_Description` |
