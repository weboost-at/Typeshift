# Status

Where the rewrite stands: what is built, which test covers each behavior, and what is open. Open items live here,
never as TODOs in code. The approved API is in [`signoff/`](https://github.com/weboost-at/Typeshift/tree/claude/rewrite/signoff);
the slices build it one at a time.

## Slices

| Slice | State |
|---|---|
| 1. Toolsets and tools: generated functions, overrides, tool lists, the analyzer | built |
| 2. Agents, registration, `AgentHost` runs | next |
| 3. Settings store, id type | open |
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

## Known gaps

- **Recursive parameter types:** each parameter's schema is created on its own and nested under the function's
  `properties`, so a `$ref` that `AIJsonUtilities.CreateJsonSchema` emits for a recursive type is relative to the
  parameter. Untested; non-recursive types are unaffected.
- **Tool names across assemblies:** TSH007 sees the toolsets of one compilation; referenced assemblies join with the
  stamps of slice 2.
- **Generated line length:** generated code is `global::`-qualified and some declarations exceed 120 columns.
- **Streamed results:** a tool returning `IAsyncEnumerable<T>` compiles, but its result is serialized synchronously
  and the call fails at runtime. Neither v0 nor the sign-off supports it.
- **Storable types:** TSH012 (a configurable member the store cannot keep) comes with the store of slice 3; until
  then any type may be configurable.
