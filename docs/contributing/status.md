# Status

Where the project stands: what is built, which test covers each behavior of the spec, and what is open. Open items
live here, never as TODOs in code.

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
- [x] Generator: toolset parsing and emitting, toolset diagnostics
- [x] Tests: toolset snapshot, one test per diagnostic, behaviors 1-9

### Milestone 3: registration (behaviors 10, 30-35)

- [x] Assembly modules and stamps, the `AddAgentic()` interceptor
- [x] The tool catalog, cross-assembly registration
- [x] Tests: module and interceptor snapshots, cross-assembly stamps, `InternalsVisibleTo`, behaviors 10, 30-35

### Milestone 4: agents and settings (behaviors 11-29, 36-37)

- [x] `AgentDefinition`, the settings layering, `AgentRuntime<TAgent>`
- [x] Tests: agent snapshot, behaviors 11-29, 36-37

### Milestone 5: stored agents (behaviors 38-40)

- [x] Built, then dropped in milestone 8 (D32)

### Milestone 6: conversations (behaviors 41-48)

- [x] The port of the conversation tracking, on `TimeProvider`
- [x] `AgentRuntime` chat clients track their conversations under the agent's name

### Milestone 7: AOT, packaging, docs (behavior 49)

- [x] The sample, the AOT CI job, the packaging smoke test
- [x] Architecture, diagnostics, decisions and renaming docs, README
- [x] Final review pass, clean re-verification, pull request

### Milestone 8: the owner's review (D25-D36)

- [x] One store, `IAgenticStore<TId>`, of `IStorable` documents; the host picks the id type; the store is the
      persistence feature flag
- [x] Generated, typed `Settings` per agent and `{Tool}Overrides` per tool, serialized without reflection
- [x] Generated tool selection and `WithToolSettings` overloads; foreign tools and unread settings do not compile
      (the overloads became generated builders in milestone 12)
- [x] Optional agent names; stored agents and the agent catalog removed; conversation ids parsed into the store's ids
- [x] Full XML docs on the public API: parameters, returns, exceptions

### Milestone 9: documentation site

- [x] VitePress site on GitHub Pages; snippets taken from compiled code by mdsnippets and checked in CI
- [x] README rewritten

### Milestone 10: distribution

- [x] NuGet release workflow on version tags, package validation, symbols, release docs
- [ ] **Blocker:** choose a package ID; `Agentic` is taken on nuget.org (D38); candidates in [naming](./naming.md)

### Milestone 11: working on the library

- [x] Playbooks for recurring changes, decision IDs cited in code, guard tests for the repository's own rules

### Milestone 12: one chain per declaration (D40-D42)

- [x] `WithSettings<T>().Configurable(...)`, `WithChatOptions().With...()`, `UseToolset<T>().A()`: no builder inside a
      lambda
- [x] Settings arrive as a tool parameter the model never sees; `GetToolSettings<T>()` removed
- [x] `GetInstructions()`; the generated `Configure{Tool}()` builder per tool of an agent
- [x] `UseToolset<T>().Include(t => t.X)` and `.Exclude(t => t.X)` over a generated tool list (D29)
- [x] `AllConfigurable()` with `NotConfigurable(...)` next to the default allow list (D43)

## Behavior coverage

Every rule of `spec/behaviors.md` maps to one test or more; a behavior that bundles several rules has one test per
rule. Where the owner's review changed a behavior, the row says how.

| # | Test |
|---|---|
| 1 | `ToolsetTests.Given_A_Tool_With_Settings_Its_Schema_Holds_Only_The_Model_Facing_Parameters` |
| 2 | `ToolsetTests.Given_No_Settings_The_Tool_Runs_With_Its_Code_Defaults` |
| 3 | `ToolsetTests.Given_Overrides_The_Tool_Runs_With_Them_Over_Its_Code_Defaults`; members that are not configurable do not exist on the overrides (D27) |
| 4 | `ToolInvocationTests.Given_A_Scripted_Call_The_Tool_Receives_Its_Arguments`, `..._Given_Two_Scripted_Calls_Each_Runs_In_A_Scope_Of_Its_Own`, `..._Given_A_Scripted_Call_The_Tool_Runs_With_Its_Resolved_Settings`, `..._Given_A_Scripted_Call_The_Result_Reaches_The_Model_As_Json` |
| 5 | `ToolInvocationTests.Given_A_Missing_Required_Argument_The_Call_Fails_With_An_ArgumentException`, `..._Given_An_Absent_Optional_Argument_The_Tool_Takes_Its_Default` |
| 6 | `ToolInvocationTests.Given_A_Struct_Parameter_Defaulting_To_Default_The_Tool_Runs_Without_It`, `ToolSchemaTests.Given_A_Struct_Parameter_Defaulting_To_Default_Its_Schema_Carries_The_Default` |
| 7 | `ToolSchemaTests.Given_A_Nullable_Reference_Parameter_It_Is_Typed_As_Nullable` |
| 8 | `ToolSchemaTests.Given_The_Same_Method_The_Schema_Matches_AIFunctionFactory_Without_Descriptions` |
| 9 | `ToolSchemaTests.Given_Param_Docs_They_Become_The_Parameter_Descriptions`, `..._Given_No_WithDescription_The_Summary_Is_The_Tool_Description` |
| 10 | `DiagnosticTests.Given_Two_Own_Tools_Sharing_A_Name_AGT007_Is_Reported_On_The_First`, `..._Given_A_Tool_Named_Like_One_Of_A_Reference_AGT007_Is_Reported_On_The_Own_One` |
| 11 | `AgentDefinitionTests.Given_Selected_Tools_And_A_Whole_Toolset_The_Agent_Offers_Exactly_Those_In_Order` |
| 12 | `DiagnosticTests.Given_A_Tool_Of_Another_Toolset_Selecting_It_Does_Not_Compile`, `..._Given_A_Toolset_Not_Compiled_With_The_Generator_AGT006_Is_Reported_On_UseToolset`, `AgentDefinitionTests.Given_A_Selected_Name_That_Is_No_Tool_Of_The_Toolset_It_Throws_An_ArgumentException` |
| 13 | `AgentDefinitionTests.Given_A_Tool_The_Agent_Already_Offers_Adding_It_Again_Throws_An_InvalidOperationException` |
| 14 | `AgentRuntimeTests.Given_Run_Settings_Their_Chat_Options_Apply_Over_The_Code`; options that are not configurable do not exist on `Settings` (`AgentSettingsTests.Exposes_Only_The_Model_The_Configurable_Chat_Options_And_The_Configurable_Tools`) |
| 15 | `AgentRuntimeTests.Given_An_Unset_Run_Option_The_Agents_Default_Stays` |
| 16 | `AgentRuntimeTests.Given_A_Conversation_Id_The_Options_Carry_It` |
| 17 | `AgentRuntimeTests.Given_A_Run_The_Agent_Runs_In_Its_Own_Execution_Context` |
| 18 | `AgentRuntimeTests.Given_The_Agents_Tool_Settings_Its_Tools_Run_With_All_Of_Them` |
| 19 | `AgentRuntimeTests.Given_Run_Tool_Settings_Their_Values_Apply_Over_The_Agents_Own` |
| 20 | `AgentRuntimeTests.Given_Run_Tool_Settings_The_Next_Run_Uses_The_Agents_Own` |
| 21 | `DiagnosticTests.Given_Settings_Of_A_Tool_The_Agent_Does_Not_Offer_Configuring_Them_Does_Not_Compile` (a compiler error since D30) |
| 22 | Changed by D31, names are optional and free-form: `AgentDefinitionTests.Given_WithName_The_Agent_Goes_By_That_Name`, `..._Given_No_WithName_The_Agent_Goes_By_Its_Class_Name` |
| 23 | Dropped by D31: names need not be unique |
| 24 | `AgentSettingsTests.Exposes_Only_The_Model_The_Configurable_Chat_Options_And_The_Configurable_Tools`, `..._Given_Values_Configured_Under_An_Id_The_Run_Resolves_With_Them`, `..._Given_Settings_Saved_Under_An_Id_They_Load_Back_Equal`, `..._Given_No_Settings_Saved_Under_An_Id_Loading_Returns_Empty_Settings`, `..._Given_Settings_Under_Two_Ids_Each_Run_Resolves_With_Its_Own` |
| 25 | `SettingsLayeringTests.Given_Stored_Tool_Defaults_And_No_Code_Settings_The_Defaults_Apply`, `..._Given_Tool_Defaults_Stored_Under_Another_Id_They_Do_Not_Apply` |
| 26 | `SettingsLayeringTests.Given_Stored_Tool_Defaults_And_Code_Settings_Of_The_Agent_The_Defaults_Do_Not_Apply` |
| 27 | `SettingsLayeringTests.Given_Stored_Agent_Settings_They_Beat_The_Code_Settings_For_Configurable_Members_Only` |
| 28 | `SettingsLayeringTests.Given_Run_Settings_They_Beat_The_Stored_Settings` |
| 29 | `SettingsLayeringTests.Given_A_Stored_Value_Set_Back_To_Null_The_Layer_Below_Applies_Again` |
| 30 | `AddAgenticTests.Registers_The_Toolsets_Of_The_Calling_Assembly`, `..._Registers_The_Toolsets_Of_A_Referenced_Assembly`, `..._Registers_The_Agents_Of_The_Calling_Assembly`, `..._Registers_The_Agents_Of_A_Referenced_Assembly`, `..._Registers_A_Runtime_For_Every_Agent` |
| 31 | `AddAgenticTests.Given_ValidateOnBuild_And_ValidateScopes_The_Container_Builds` |
| 32 | `InterceptorGenerationTests.Given_InternalsVisibleTo_A_Generated_Project_The_Tests_Compile_Their_AddAgentic_Call` |
| 33 | `AddAgenticTests.Given_A_Call_That_Is_Not_Intercepted_It_Throws_What_To_Do` |
| 34 | `ToolCatalogTests.Given_The_Name_Of_A_Tool_Of_A_Referenced_Assembly_It_Creates_That_Tool`, `..._Given_Overrides_The_Created_Tool_Runs_With_Them` |
| 35 | Changed by D32, the agent catalog is gone: every agent's runtime is listed as `AgentRuntime` (`AddAgenticTests.Registers_A_Runtime_For_Every_Agent`) |
| 36 | `NoStoreTests.Given_No_Store_Resolving_A_Runs_Options_Works_From_The_Code_Alone` |
| 37 | `NoStoreTests.Given_No_Store_Saving_Settings_Throws_What_To_Register`, `..._Given_No_Store_Resolving_With_An_Id_Throws_What_To_Register` |
| 38-40 | Dropped by D32: agents as data |
| 41 | `TrackedChatClientTests.Given_A_Completed_Session_It_Stores_The_Input_And_Every_Message_The_Model_Produced` |
| 42 | `TrackedChatClientTests.Given_A_Failing_Inner_Client_The_Exception_Surfaces`, `..._Given_A_Failing_Inner_Client_The_Session_Is_Stored_With_An_Error`, `..._Given_A_Failing_Inner_Client_No_Session_Stays_Tracked` |
| 43 | `TrackedChatClientTests.Given_A_Cancelled_Request_The_Stored_Error_Says_It_Was_Cancelled` |
| 44 | `TrackedChatClientTests.Given_An_Id_That_Is_Not_Stored_A_New_Conversation_Starts_Under_It`; with D33, `..._Given_No_Conversation_Id_The_Session_Is_Tracked_Without_One`, `..._Given_A_Conversation_Id_The_Store_Cannot_Parse_The_Conversation_Is_Not_Resumed` |
| 45 | `TrackedChatClientTests.Given_A_Stored_Conversation_The_Model_Receives_The_Earlier_Messages_And_The_New_Input`, `..._Given_A_Resumed_Conversation_Its_Usage_Is_Aggregated_Over_Its_Sessions`, `..._Given_A_Stored_Call_Without_A_Result_The_Resumed_Conversation_Leaves_It_Out`, `StoredConversationTests.Given_A_Stored_Conversation_It_Round_Trips_Through_Json`, `AgentRuntimeTests.Given_A_Model_Its_Chat_Client_Stores_The_Conversation_Under_The_Agent` |
| 46 | `TrackedChatClientStreamingTests.Given_A_Consumed_Stream_It_Stores_The_Response_Of_Every_Update`, `..._Given_A_Consumer_That_Stops_Early_It_Stores_What_Arrived`, `..._Given_A_Cancelled_Stream_It_Stores_What_Arrived` |
| 47 | `RealtimeTrackedConversationSessionTests.Given_A_Notable_Update_It_Is_Published_Before_The_Consumer_Sees_It`, `..._Given_Text_Within_The_Interval_It_Is_Not_Published_Yet`, `..._Given_Text_After_The_Interval_The_Next_Delta_Is_Published_Before_The_Consumer_Sees_It` |
| 48 | `SubscriberTests.Given_Throwing_Subscribers_The_Stream_Completes`, `..._Given_A_Subscriber_That_Never_Finishes_The_Call_Goes_On_At_The_Limit`, `..._Given_A_Session_Subscriber_That_Never_Finishes_The_Stream_Goes_On_At_The_Limit`, `..._Given_A_Subscriber_Its_Token_Is_Cancelled_At_The_Limit` |
| 49 | CI, `.github/workflows/ci.yml`: "Publish the sample as Native AOT, failing on any trim or AOT warning" and "Run the AOT sample" |

Beyond the behaviors: `StorableJsonTests` cover every storable member kind, nulls, enums by name and malformed
values; AGT014 has a test for settings and one for chat options; the review pass's regression tests stay
(referenced tools with optional parameters, tools in an agent's own chat options, the conversation id kept from the
provider, overlapping first sessions, a parameter documented twice, nested and internal agents, reserved names,
moved code staying cached). CI builds and runs `samples/Agentic.Samples.PackageConsumer` against the packed
`.nupkg`.

## Environment notes

- The run had the .NET 10 SDK and `clang`, so the AOT publish and the package consumer ran locally as well as in CI.
- The session's git proxy answers HTTP 403 to every tag push, so no release tag exists yet. The first release follows
  [releasing](./releasing.md) once the package ID is chosen.

## Known gaps

- **Recursive parameter types:** each parameter's schema is created on its own and nested under the function's
  `properties`, so a `$ref` that `AIJsonUtilities.CreateJsonSchema` emits for a recursive type is relative to the
  parameter, not to the function schema. Untested; non-recursive types are unaffected.
- **Private nested classes:** a toolset or agent nested as `private` or `protected` fails with CS0122 in the
  generated module instead of a diagnostic. Nested classes the assembly can reach work.
- **Agent inheritance:** an agent deriving from another concrete agent is not supported: the generator reads only the
  agent's own constructor, and both classes would declare a nested `Settings`.
- **Store round trips:** resolving with an id loads the agent's settings and one document per tool with configurable
  settings, in parallel. A store that wants one round trip has no batch API to implement yet.
- **Concurrent updates:** `ConfigureAsync` loads, changes and saves without a version check, so of two concurrent
  updates under one id the later save wins.
- **Document kinds:** stores key documents by type, and the library offers no stable kind name, so moving or
  renaming an agent class orphans its settings in a store keyed by the type's full name.
- **Renamed members:** stored documents carry no schema version. A renamed configurable member or tool reads as
  null, so its stored value silently falls back to the layer below.
- **Configurable types:** only the types AGT014 lists; nested records, dictionaries and lists of other types stay
  code-only.
- **AGT011 reads types:** to find a reference that declares toolsets or agents without a stamp, the generator looks
  at the types of every reference that uses the library but carries no stamp. Stamped references, the normal case,
  are read from their stamps only.
- **No JSON-context analyzer:** a tool whose parameter or result type is missing from every `JsonSerializerContext`
  compiles, then fails at runtime under Native AOT.
- **Fixed tracking limits:** the publish interval (100 ms) and the subscriber limits (250 ms per session, 5 s for the
  tracker) are constants, as in the owner's code.
- **Generated line length:** generated code is `global::`-qualified and some declarations exceed 120 columns.
- **Selection chains are read from syntax:** `UseToolset<T>().Include(t => t.A)` must be one expression, and each
  pick a lambda reading one property. Splitting it, `var tickets = UseToolset<T>(); tickets.Include(t => t.A);`,
  offers only `A` at runtime while the generator assumes every tool of `T` (D29).

## Next steps

1. Store generators for Marten and EF Core: the stored documents are a closed set of known types (D25).
2. Versioned documents with stable kind names, and optimistic concurrency for stored settings.
3. The JSON-context analyzer: report tool parameter and result types that no registered context covers.
4. Make the tracking limits options.
5. A diagnostic for toolsets and agents the module cannot reach.
6. Rewrite `$ref`s of recursive parameter types, with a test against `AIFunctionFactory`.
7. Instance-method tools, if wanted (out of the alpha's scope).
8. A sample provider behind an environment variable, once provider SDKs are in scope.

## Decisions to review first

From the [decisions](./decisions.md), the ones that shape the API the most:

1. **D25, D26:** one store of self-serializing documents, keyed by type and the host's id; without it the
   persistent APIs throw.
2. **D27:** the generated `Settings` and `{Tool}Overrides` records, their JSON shape and the storable member types.
3. **D28:** tool defaults are stored under the agent's id.
4. **D29, D30:** the tool lists and `Configure{Tool}()` are generated, so misuse is a compiler error.
5. **D40, D41, D42:** settings arrive as a tool parameter; every declaration is one flat chain; instructions come
   from `GetInstructions()`.
6. **D33:** conversation ids are parsed into the store's id type; a call without one is not stored.
7. **D1, D4:** `AddAgentic()` exists only as an intercepted call; the typed runtime members are C# 14 extension
   blocks.
8. **D37, D38:** releases are tags published through nuget.org trusted publishing; the package ID is still to be
   chosen.
