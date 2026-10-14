# Status

Where the rewrite stands: what is built, which test covers each behavior, and what is open. Open items live here,
never as TODOs in code. The approved API is in [`signoff/`](https://github.com/weboost-at/Typeshift/tree/claude/rewrite/signoff);
the slices build it one at a time.

## Slices

| Slice | State |
|---|---|
| 1. Toolsets and tools: generated functions, overrides, tool lists, the analyzer | built |
| 2. Agents, registration, `AgentHost` runs | built |
| 3. Settings store, id type | next |
| 4. Conversations: checkpoints, resume, live updates, steering | open |
| 5. Approval, rules, plan and auto modes | open |
| 6. Middleware, context providers, MCP, telemetry, compaction, sub-agents, handoff, built-in toolsets, Native AOT | open |

## Behavior coverage

The numbers are the rules of v0's behavior table where they still hold.

| # | Behavior | Test |
|---|---|---|
| 1 | A tool's schema holds only the model-facing parameters | `ToolSchemaTests.Given_Parameters_The_Call_Fills_They_Stay_Out_Of_The_Schema` |
| 3 | Overrides apply over the settings below; members that are not configurable have no override | `ToolOverridesTests` |
| 4 | A call receives its arguments, its settings, the run's services and the call's token; the result reaches the model as JSON | `ToolInvocationTests.Given_A_Scripted_Call_The_Tool_Receives_Its_Arguments`, `..._Given_Settings_The_Tool_Runs_With_Them`, `..._Given_Two_Calls_Of_One_Run_Both_Resolve_From_The_Runs_Scope`, `..._Given_A_Call_The_Tool_Receives_The_Calls_Token`, `..._Given_A_Scripted_Call_The_Result_Reaches_The_Model_As_Json` |
| 5 | A missing required argument fails the call; an absent optional one takes its default | `ToolInvocationTests.Given_A_Missing_Required_Argument_The_Call_Fails_With_An_ArgumentException`, `..._Given_An_Absent_Optional_Argument_The_Tool_Takes_Its_Default` |
| 6 | A struct parameter defaulting to `default` | `ToolInvocationTests.Given_A_Struct_Parameter_Defaulting_To_Default_The_Tool_Runs_Without_It`, `ToolSchemaTests.Given_A_Struct_Parameter_Defaulting_To_Default_Its_Schema_Carries_The_Default` |
| 7 | A nullable reference parameter is typed nullable | `ToolSchemaTests.Given_A_Nullable_Reference_Parameter_It_Is_Typed_As_Nullable` |
| 8 | The schema matches `AIFunctionFactory`'s | `ToolSchemaTests.Given_The_Same_Method_The_Schema_Matches_AIFunctionFactory_Without_Descriptions` |
| 9 | Docs become descriptions | `ToolSchemaTests.Given_Param_Docs_They_Become_The_Parameter_Descriptions`, `..._Given_No_WithDescription_The_Summary_Is_The_Tool_Description` |
| 10 | Tool names are unique | `DiagnosticTests.Given_Two_Tools_Mapping_To_One_Member_TSH007_Is_Reported_On_The_Second`, `..._Given_Two_Toolsets_With_A_Tool_Of_One_Name_TSH007_Is_Reported_On_The_Second` |
| new | Tool names derive from the method, without `Async` (D13) | `ToolSchemaTests.Given_An_Async_Method_Without_WithName_The_Tool_Is_Named_Without_The_Suffix`, `ToolNamesTests` |
| new | A call without services fails, saying how to run the tool | `ToolInvocationTests.Given_No_Services_The_Call_Fails_Saying_How_To_Run_It` |
| new | Every shape the generator cannot handle is a diagnostic, and the generated code holds no error | `DiagnosticTests`, `DiagnosticTests.Given_Any_Mistake_The_Generated_Code_Holds_No_Error` |
| new | Parameters the model never sees need no `<param>` | `ToolsetGenerationTests.Given_Parameters_The_Model_Never_Sees_Their_Missing_Docs_Are_Suppressed` |
| 11 | `AddTypeshift()` registers the project's and the references' toolsets and agents, once | `AddTypeshiftTests` |
| 12 | A call the generator did not intercept throws, saying what to check | `AddTypeshiftTests.Given_A_Call_The_Generator_Did_Not_Intercept_It_Fails_Saying_What_To_Check` |
| 13 | A run gets the agent's code options; run settings override the configurable ones, null keeps the code | `AgentRunTests.Given_A_Run_The_Model_Receives_The_Agents_Code_Options`, `..._Given_Run_Settings_They_Override_The_Configurable_Options`, `..._Given_Run_Settings_That_Leave_An_Option_Null_The_Agents_Code_Holds` |
| 14 | An agent offers its toolsets' tools, included in pick order or all but the excluded, then its own | `AgentRunTests.Given_The_Agents_Toolsets_The_Model_Is_Offered_Their_Tools_Then_Its_Own`, `..._Given_Exclude_Every_Other_Tool_Is_Offered_In_Declaration_Order` |
| 15 | Tool settings layer: code defaults, `Configure{Tool}()`, the run's overrides | `AgentRunTests.Given_Configure_The_Tool_Runs_With_The_Agents_Settings`, `..._Given_Run_Overrides_Of_A_Tool_They_Apply_Over_The_Agents_Settings` |
| new | A run's tools resolve from the scope its host came from (D25) | `AgentRunTests.Given_Two_Tool_Calls_Of_A_Run_Both_Resolve_From_The_Hosts_Scope` |
| new | Messages and updates carry the agent's name (D26) | `AgentRunTests.Given_A_Run_Its_Messages_Carry_The_Agents_Name_As_Their_Author`, `..._Given_A_Streaming_Run_Its_Updates_Carry_The_Text_And_The_Agents_Name` |
| new | Run options limit the tool loop and may be configurable (D32) | `AgentRunTests.Given_Run_Settings_Of_A_Run_Option_The_Tool_Loop_Stops_After_It` |
| new | Structured output and the raw path | `AgentRunTests.Given_A_Result_Type_The_Run_Returns_The_Models_Json_Typed`, `..._Given_Resolved_Options_The_Created_Client_Runs_The_Agent` |
| new | A run without a model or a provider fails, saying how to fix it | `AgentRunTests.Given_No_Model_The_Run_Fails_Saying_How_To_Set_One`, `AddTypeshiftTests.Given_No_Chat_Client_Provider_A_Run_Fails_Naming_The_Registration` |
| new | Toolsets of referenced assemblies: stamps, settings, JSON contexts (D7, D45) | `AgentRunTests.Given_A_Toolset_Of_A_Referenced_Assembly_Its_Tool_Runs_With_Its_Settings`, `AddTypeshiftTests.Given_A_Json_Context_In_A_Toolsets_Assembly_Tools_Ask_It_Before_The_Defaults`, `AgentGenerationTests` |
| new | An agent's output is generated again only when what it offers changes | `IncrementalTests` |

## Known gaps

- **Recursive parameter types:** each parameter's schema is created on its own and nested under the function's
  `properties`, so a `$ref` that `AIJsonUtilities.CreateJsonSchema` emits for a recursive type is relative to the
  parameter. Untested; non-recursive types are unaffected.
- **Tool names across references:** TSH007 compares the compilation's tools with each other and with the tools the
  references stamp, not two references' tools with each other. An agent offering two referenced tools of one name
  keeps the first; the generated code stays valid.
- **Generated line length:** generated code is `global::`-qualified and some declarations exceed 120 columns.
- **Streamed results:** a tool returning `IAsyncEnumerable<T>` compiles, but its result is serialized synchronously
  and the call fails at runtime. Neither v0 nor the sign-off supports it.
- **Storable types:** TSH012 (a configurable member the store cannot keep) comes with the store of slice 3; until
  then any type may be configurable.
