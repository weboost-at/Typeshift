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

- [x] Assembly modules and stamps, the `AddAgentic()` interceptor
- [x] Tool and agent catalogs, cross-assembly registration
- [x] AGT008, AGT014
- [x] Tests: module and interceptor snapshots, cross-assembly stamps, `InternalsVisibleTo`, behaviors 10, 30-35

### Milestone 4: agents and settings (behaviors 11-29, 36-37)

- [x] `AgentDefinition`, the typed configuration facades, the settings layering, `AgentRuntime<TAgent>`
- [x] Store interfaces with in-memory implementations
- [x] Agent diagnostics (AGT006, AGT007, AGT010, AGT012)
- [x] Tests: agent snapshot, behaviors 11-29, 36-37

### Milestone 5: stored agents (behaviors 38-40)

- [x] The stored-agent record, the export from code agents, catalog integration

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
| 10 | `DiagnosticTests.Given_Two_Own_Tools_Sharing_A_Name_AGT008_Is_Reported_On_The_First`, `..._Given_A_Tool_Named_Like_One_Of_A_Reference_AGT008_Is_Reported_On_The_Own_One` (generator tests) |
| 11 | `AgentDefinitionTests.Given_Selected_Tools_And_A_Whole_Toolset_The_Agent_Offers_Exactly_Those_In_Order` |
| 12 | `AgentDefinitionTests.Given_A_Method_Of_Another_Toolset_Selecting_It_Throws_An_ArgumentException`, `DiagnosticTests.Given_A_Method_Of_Another_Toolset_AGT007_Is_Reported_On_It`, `..._Given_A_Toolset_Not_Compiled_With_The_Generator_AGT007_Is_Reported_On_UseToolset` |
| 13 | `AgentDefinitionTests.Given_A_Tool_The_Agent_Already_Offers_Adding_It_Again_Throws_An_InvalidOperationException` |
| 14 | `AgentRuntimeTests.Given_Run_Chat_Options_Only_The_Allowlisted_Ones_Change` |
| 15 | `AgentRuntimeTests.Given_An_Unset_Allowlisted_Run_Option_The_Agents_Default_Stays` |
| 16 | `AgentRuntimeTests.Given_Run_Chat_Options_With_A_Conversation_The_Conversation_Id_Is_Kept` |
| 17 | `AgentRuntimeTests.Given_Run_Chat_Options_Of_Another_Execution_Context_The_Agent_Runs_In_Its_Own` |
| 18 | `AgentRuntimeTests.Given_The_Agents_Tool_Settings_Its_Tools_Run_With_All_Of_Them` |
| 19 | `AgentRuntimeTests.Given_Run_Tool_Settings_Only_Their_Allowlisted_Members_Change` |
| 20 | `AgentRuntimeTests.Given_Run_Tool_Settings_The_Next_Run_Uses_The_Agents_Own` |
| 21 | `AgentDefinitionTests.Given_Settings_No_Tool_Of_The_Agent_Reads_WithToolSettings_Throws_An_ArgumentException`, `DiagnosticTests.Given_Tool_Settings_No_Tool_Of_The_Agent_Reads_AGT012_Is_Reported_On_Them` |
| 22 | `AgentDefinitionTests.Given_A_Name_That_Is_Not_Lower_Snake_Case_WithUniqueName_Throws_An_ArgumentException`, `DiagnosticTests.Given_A_Unique_Name_That_Is_Not_Lower_Snake_Case_AGT006_Is_Reported_On_It` |
| 23 | `DiagnosticTests.Given_An_Agent_Without_WithUniqueName_AGT006_Is_Reported_On_Its_Name`, `..._Given_Two_Agents_Sharing_A_Unique_Name_AGT010_Is_Reported_On_The_First`, `AgentCatalogTests.Given_Two_Agents_With_One_Unique_Name_It_Throws_An_InvalidOperationException` |
| 24 | `AgentConfigurationViewTests.Exposes_Only_The_Allowlisted_Chat_Options_And_The_Configurable_Tools`, `..._Given_Values_Written_Through_The_View_The_Run_Resolves_With_Them` |
| 25 | `SettingsLayeringTests.Given_Stored_Tool_Defaults_And_No_Code_Settings_The_Defaults_Apply` |
| 26 | `SettingsLayeringTests.Given_Stored_Tool_Defaults_And_Code_Settings_Of_The_Agent_The_Defaults_Do_Not_Apply` |
| 27 | `SettingsLayeringTests.Given_Stored_Agent_Overrides_They_Beat_The_Code_Settings_For_Allowlisted_Members_Only` |
| 28 | `SettingsLayeringTests.Given_Run_Overrides_They_Beat_The_Stored_Overrides_For_Allowlisted_Members_Only` |
| 29 | `SettingsLayeringTests.Given_A_Stored_Value_Set_Back_To_Null_The_Layer_Below_Applies_Again` |
| 30 | `AddAgenticTests.Registers_The_Toolsets_Of_The_Calling_Assembly`, `..._Registers_The_Toolsets_Of_A_Referenced_Assembly`, `..._Registers_The_Agents_Of_The_Calling_Assembly`, `..._Registers_The_Agents_Of_A_Referenced_Assembly` |
| 31 | `AddAgenticTests.Given_ValidateOnBuild_And_ValidateScopes_The_Container_Builds` |
| 32 | `InterceptorGenerationTests.Given_InternalsVisibleTo_A_Generated_Project_The_Tests_Compile_Their_AddAgentic_Call` (generator tests) |
| 33 | `AddAgenticTests.Given_A_Call_That_Is_Not_Intercepted_It_Throws_What_To_Do` |
| 34 | `ToolCatalogTests.Given_The_Name_Of_A_Tool_Of_A_Referenced_Assembly_It_Creates_That_Tool`, `..._Given_Stored_Settings_The_Created_Tool_Runs_With_Them` |
| 35 | `AgentCatalogTests.Lists_Every_Code_Agent_By_Unique_Name` |
| 36 | `NoStoreTests.Given_No_Store_Resolving_A_Runs_Options_Works_From_The_Code_Alone` |
| 37 | `NoStoreTests.Given_No_Store_Updating_A_Configuration_Throws_What_To_Register` |
| 38 | `StoredAgentTests.Given_A_Code_Agent_Its_Exported_Record_Round_Trips_Through_Json`, `..._Given_An_Exported_Code_Agent_Restored_From_Json_It_Offers_The_Same_Tools`, `..._Given_An_Exported_Code_Agent_Restored_From_Json_Its_Tools_Run_With_The_Same_Settings` |
| 39 | `StoredAgentTests.Given_A_Stored_Agent_Naming_An_Unknown_Tool_Creating_Its_Runtime_Throws_Which_Tool` |
| 40 | `StoredAgentTests.Given_A_New_Stored_Agent_Its_Generated_Name_Can_Never_Be_A_Code_Agents_Name`, `..._Given_A_Stored_Agent_Named_Like_A_Code_Agent_Saving_It_Throws` |
