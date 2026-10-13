# Status

Where the project stands: which test covers each behavior of the alpha's spec, and what is open. Open items live
here, never as TODOs in code.

## Behavior coverage

The numbers are the rules of the alpha's spec, `spec/behaviors.md`, which is not in the repository. Every rule maps
to one test or more; a behavior that bundles several rules has one test per rule. Where the owner's review changed a
behavior, the row says how.

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
| 10 | `DiagnosticTests.Given_Two_Own_Tools_Sharing_A_Name_TSH007_Is_Reported_On_The_First`, `..._Given_A_Tool_Named_Like_One_Of_A_Reference_TSH007_Is_Reported_On_The_Own_One` |
| 11 | `AgentDefinitionTests.Given_Included_Tools_And_A_Whole_Toolset_The_Agent_Offers_Exactly_Those_In_Order` |
| 12 | `DiagnosticTests.Given_A_Tool_Of_Another_Toolset_Including_It_Does_Not_Compile`, `..._Given_A_Toolset_Not_Compiled_With_The_Generator_TSH006_Is_Reported_On_UseToolset`, `AgentDefinitionTests.Given_An_Included_Name_That_Is_No_Tool_Of_The_Toolset_It_Throws_An_ArgumentException` |
| 13 | `AgentDefinitionTests.Given_A_Tool_The_Agent_Already_Offers_Adding_It_Again_Throws_An_InvalidOperationException` |
| 14 | `AgentRuntimeTests.Given_Run_Settings_Their_Chat_Options_Apply_Over_The_Code`; options that are not configurable do not exist on `Settings` (`AgentSettingsTests.Exposes_Only_The_Model_The_Configurable_Chat_Options_And_The_Configurable_Tools`) |
| 15 | `AgentRuntimeTests.Given_An_Unset_Run_Option_The_Agents_Default_Stays` |
| 16 | `AgentRuntimeTests.Given_A_Conversation_Id_The_Options_Carry_It` |
| 17 | `AgentRuntimeTests.Given_A_Run_The_Agent_Runs_In_Its_Own_Execution_Context` |
| 18 | `AgentRuntimeTests.Given_The_Agents_Tool_Settings_Its_Tools_Run_With_All_Of_Them` |
| 19 | `AgentRuntimeTests.Given_Run_Tool_Settings_Their_Values_Apply_Over_The_Agents_Own` |
| 20 | `AgentRuntimeTests.Given_Run_Tool_Settings_The_Next_Run_Uses_The_Agents_Own` |
| 21 | `DiagnosticTests.Given_Settings_Of_A_Tool_The_Agent_Does_Not_Offer_Configuring_Them_Does_Not_Compile`, `..._Given_Settings_Of_An_Excluded_Tool_Configuring_Them_Does_Not_Compile` (a compiler error since D30) |
| 22 | Changed by D31, names are optional and free-form: `AgentDefinitionTests.Given_WithName_The_Agent_Goes_By_That_Name`, `..._Given_No_WithName_The_Agent_Goes_By_Its_Class_Name` |
| 23 | Dropped by D31: names need not be unique |
| 24 | `AgentSettingsTests.Exposes_Only_The_Model_The_Configurable_Chat_Options_And_The_Configurable_Tools`, `..._Given_Values_Configured_Under_An_Id_The_Run_Resolves_With_Them`, `..._Given_Settings_Saved_Under_An_Id_They_Load_Back_Equal`, `..._Given_No_Settings_Saved_Under_An_Id_Loading_Returns_Empty_Settings`, `..._Given_Settings_Under_Two_Ids_Each_Run_Resolves_With_Its_Own` |
| 25 | `SettingsLayeringTests.Given_Stored_Tool_Defaults_And_No_Code_Settings_The_Defaults_Apply`, `..._Given_Tool_Defaults_Stored_Under_Another_Id_They_Do_Not_Apply` |
| 26 | `SettingsLayeringTests.Given_Stored_Tool_Defaults_And_Code_Settings_Of_The_Agent_The_Defaults_Do_Not_Apply` |
| 27 | `SettingsLayeringTests.Given_Stored_Agent_Settings_They_Beat_The_Code_Settings_For_Configurable_Members_Only` |
| 28 | `SettingsLayeringTests.Given_Run_Settings_They_Beat_The_Stored_Settings` |
| 29 | `SettingsLayeringTests.Given_A_Stored_Value_Set_Back_To_Null_The_Layer_Below_Applies_Again` |
| 30 | `AddTypeshiftTests.Registers_The_Toolsets_Of_The_Calling_Assembly`, `..._Registers_The_Toolsets_Of_A_Referenced_Assembly`, `..._Registers_The_Agents_Of_The_Calling_Assembly`, `..._Registers_The_Agents_Of_A_Referenced_Assembly`, `..._Registers_A_Runtime_For_Every_Agent` |
| 31 | `AddTypeshiftTests.Given_ValidateOnBuild_And_ValidateScopes_The_Container_Builds` |
| 32 | `InterceptorGenerationTests.Given_InternalsVisibleTo_A_Generated_Project_The_Tests_Compile_Their_AddTypeshift_Call` |
| 33 | `AddTypeshiftTests.Given_A_Call_That_Is_Not_Intercepted_It_Throws_What_To_Do` |
| 34 | `ToolCatalogTests.Given_The_Name_Of_A_Tool_Of_A_Referenced_Assembly_It_Creates_That_Tool`, `..._Given_Overrides_The_Created_Tool_Runs_With_Them` |
| 35 | Changed by D32, the agent catalog is gone: every agent's runtime is listed as `AgentRuntime` (`AddTypeshiftTests.Registers_A_Runtime_For_Every_Agent`) |
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

Beyond the behaviors: `StorableJsonTests` cover every storable member kind, nulls, enums and flags by name, NaN and
the infinities, and malformed values; TSH014 has a test for settings and one for chat options; the review passes'
regression tests stay (referenced tools with optional parameters, tools in an agent's own chat options, the
conversation id kept from the provider, overlapping first sessions and sessions of one conversation ending at once,
a conversation deleted during a session, a parameter documented twice, nested and internal agents, reserved names,
moved code staying cached, multi-line descriptions, keyword-named namespaces and settings members, names that shadow
a cref, parameter defaults of every kind, static local functions, a toolset nested in a class that is not partial,
private settings of an internal toolset, a tool name clash among references only, and a test run outside UTC). CI
builds and runs `samples/Typeshift.Samples.PackageConsumer` against the packed `.nupkg`.

## Known gaps

- **Recursive parameter types:** each parameter's schema is created on its own and nested under the function's
  `properties`, so a `$ref` that `AIJsonUtilities.CreateJsonSchema` emits for a recursive type is relative to the
  parameter, not to the function schema. Untested; non-recursive types are unaffected.
- **Private nested classes:** a toolset or agent nested as `private` or `protected` fails with CS0122 in the
  generated module instead of a diagnostic. Nested classes the assembly can reach work.
- **No parameterless constructor:** a toolset or agent without a parameterless constructor fails with CS7036 in the
  generated module instead of a diagnostic.
- **Keyword names:** a toolset or agent named like a keyword (`@class`), or nested in such a class, is written
  without its `@` and fails with CS1001.
- **A toolset named `Tools`:** the generator nests a class of the same name in it, which fails with CS0542.
- **Tool names that differ in underscores:** `get_v2` and `get_v_2` map to the same generated name, `GetV2`, which
  fails with CS0102. TSH007 compares the snake_case names, so it does not see the clash.
- **Agent and toolset inheritance:** an agent deriving from another concrete agent is not supported: the generator
  reads only the agent's own constructor, and both classes would declare a nested `Settings`. A toolset deriving
  from another concrete toolset hides the base's generated `Tools` class (CS0108) and registers the base's tools
  twice.
- **Store round trips:** resolving with an id loads the agent's settings and one document per tool with configurable
  settings, in parallel. A store that wants one round trip has no batch API to implement yet.
- **Concurrent updates:** `ConfigureAsync` loads, changes and saves without a version check, so of two concurrent
  updates under one id the later save wins. Conversation saves (D18) have no version check either: within one
  process, the sessions of a conversation that end together are stored one after the other, but across processes
  the later save wins and the other session is lost.
- **Document kinds:** stores key documents by type, and the library offers no stable kind name, so moving or
  renaming an agent class orphans its settings in a store keyed by the type's full name.
- **Renamed members:** stored documents carry no schema version. A renamed configurable member or tool reads as
  null, so its stored value silently falls back to the layer below.
- **Configurable types:** only the types TSH014 lists; nested records, dictionaries and lists of other types stay
  code-only.
- **Configurable members need a setter:** `Configurable(s => s.X)` on a property without a public `set` or `init`
  accessor is ignored without a diagnostic, and `X` stays code-only.
- **TSH011 reads types:** to find a reference that declares toolsets or agents without a stamp, the generator looks
  at the types of every reference that uses the library but carries no stamp. Stamped references, the normal case,
  are read from their stamps only.
- **TSH diagnostics cannot be silenced in code:** `#pragma warning disable` and `.editorconfig` severities do not
  reach them, since the generator reports them at locations without a syntax tree (its models keep paths and spans,
  never trees). `<NoWarn>` in the project works.
- **No JSON-context analyzer:** a tool whose parameter or result type is missing from every registered
  `JsonSerializerContext` compiles, then fails at runtime under Native AOT.
- **JSON contexts match exactly:** a module registers a context only when its `[JsonSerializable]` names a tool type
  as declared, so one that lists only `Company` is left out for a tool returning `IReadOnlyList<Company>` (D44).
- **Fixed tracking limits:** the publish interval (100 ms) and the subscriber limits (250 ms per session, 5 s for the
  tracker) are constants, as in the owner's code.
- **Non-partial toolsets:** an agent that uses an own toolset which is not partial gets TSH006, "not compiled with
  the generator", next to the toolset's TSH001.
- **Generated line length:** generated code is `global::`-qualified and some declarations exceed 120 columns.
- **Tool picks are read from syntax:** `UseToolset<T>().Include(t => t.A)` must be one expression, and each
  pick a lambda reading one property. Splitting it, `var tickets = UseToolset<T>(); tickets.Include(t => t.A);`,
  offers only `A` at runtime while the generator assumes every tool of `T` (D29).

## Next steps

1. Store generators for Marten and EF Core: the stored documents are a closed set of known types (D25).
2. Versioned documents with stable kind names, and optimistic concurrency for stored settings.
3. The JSON-context analyzer: report tool parameter and result types that no context of the tool's assembly lists,
   now that the generator reads them (D44).
4. Make the tracking limits options.
5. A diagnostic for toolsets and agents the module cannot reach.
6. Rewrite `$ref`s of recursive parameter types, with a test against `AIFunctionFactory`.
7. Instance-method tools, if wanted (out of the alpha's scope).
8. A sample provider behind an environment variable, once provider SDKs are in scope.
