# Status

Where the rewrite stands: what is built, which test covers each behavior, and what is open. Open items live here,
never as TODOs in code. The approved API is in [`signoff/`](https://github.com/weboost-at/Typeshift/tree/claude/rewrite/signoff);
the slices build it one at a time.

## Slices

| Slice | State |
|---|---|
| 1. Toolsets and tools: generated functions, overrides, tool lists, the analyzer | built |
| 2. Agents, registration, `AgentHost` runs | built |
| 3. Settings store, id type | built |
| 4. Conversations: checkpoints, resume, live updates, steering | built |
| 5. Approval, rules, plan and auto modes | built |
| 6. Middleware, context providers | built |
| 7. Telemetry, compaction | next |
| 8. Sub-agents, handoff | open |
| 9. MCP, built-in toolsets | open |
| 10. Native AOT: the console sample, the package consumer | open |

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
| 16 | Stored settings apply under their id, run settings over them, tool overrides included | `StoredSettingsTests.Given_Stored_Settings_A_Run_With_Their_Id_Applies_Them`, `..._Given_Stored_And_Run_Settings_The_Runs_Apply_Over_The_Stored`, `..._Given_Stored_Tool_Overrides_The_Tool_Runs_With_Them_Over_The_Agents_Settings` |
| 17 | Saves are guarded by revisions; `ConfigureAsync` retries until it wins (D41) | `StoredSettingsTests.Given_A_Revision_Another_Save_Replaced_Saving_Throws_A_StoreConflictException`, `..._Given_A_Save_Between_Load_And_Save_Configuring_Applies_The_Change_Again_And_Wins` |
| 18 | Without a store, loading or saving throws naming `WithStore<T>()` (D43) | `StoredSettingsTests.Given_No_Store_A_Run_With_A_Settings_Id_Fails_Naming_WithStore` |
| 19 | Settings of every storable kind read back equivalent; nulls left out; mistyped members read as null | `SettingsJsonTests` |
| new | `System.Text.Json` writes and reads the stored shape (D44) | `SettingsJsonTests.Given_Settings_System_Text_Json_Writes_What_The_Store_Keeps`, `..._Given_Stored_Json_System_Text_Json_Reads_The_Settings` |
| new | Kinds and versions (D39, D40) | `SettingsJsonTests.Given_WithStorageKind_And_WithSettingsVersion_The_Settings_Carry_Them`, `..._Given_A_Version_Newer_Than_The_Codes_Reading_Throws_A_JsonException` |
| new | The id type reaches the aliases and the typed members (D11) | `AgentGenerationTests.Given_An_Id_Type_The_Aliases_And_The_Host_Members_Use_It` |
| 20 | A conversation's next run starts from its stored messages; the provider never sees its id | `ConversationRunTests.Given_A_Conversation_A_Second_Run_Starts_From_The_First`, `..._Given_A_Conversation_The_Provider_Never_Receives_Its_Id` |
| 21 | Each step is stored as it happens; the last entry ends the run (D50) | `ConversationRunTests.Given_A_Run_Its_Input_Responses_And_Tool_Results_Are_Stored_Step_By_Step`, `..._Given_A_Run_Only_Its_Last_Entry_Ends_It`, `..._Given_A_Model_Response_Its_Entry_Carries_The_Model_And_The_Agent` |
| 22 | A failed run ends with its error, which the next run leaves out; a stream left early is stored (D46) | `ConversationRunTests.Given_A_Failing_Run_It_Ends_With_Its_Error_And_The_Exception_Surfaces`, `..._Given_A_Failed_Run_The_Next_Run_Leaves_Out_Its_Error`, `..._Given_A_Stream_Left_Early_The_Run_Ends_With_The_Updates_That_Arrived` |
| 23 | A store that fails to append does not fail the run | `ConversationStoreTests.Given_A_Store_That_Fails_To_Append_The_Run_Still_Answers` |
| new | Resuming continues an interrupted run without the call in flight (D50) | `ConversationRunTests.Given_An_Interrupted_Run_Resuming_Continues_From_Its_Last_Step_Without_The_Call_In_Flight`, `..._Given_A_Conversation_Whose_Last_Run_Ended_Resuming_Fails` |
| new | One run per conversation in a process; the tracker lists it | `ConversationRunTests.Given_A_Run_Going_On_A_Second_Run_Of_The_Conversation_Fails`, `..._Given_A_Run_Going_On_The_Tracker_Lists_It` |
| new | Steering reaches the next model call, is stored, and keeps the run going (D51) | `ConversationRunTests.Given_Steering_Input_The_Run_Answers_It_Before_It_Ends`, `..._Given_Steering_Input_During_The_Answer_To_Steering_The_Run_Answers_Both_In_Order`, `..._Given_Steering_Input_It_Is_Stored_With_The_Conversation` |
| new | Watchers receive streamed updates (D47) | `ConversationRunTests.Given_A_Watcher_It_Receives_The_Updates_Of_A_Streaming_Run` |
| new | The raw path names its conversation in `ChatOptions.ConversationId` | `ConversationRunTests.Given_A_Conversation_Id_In_The_Options_The_Raw_Path_Stores_The_Run` |
| new | A call that requires approval ends the run with its request; the next run's answer runs or rejects it (D24) | `ToolApprovalTests.Given_A_Call_That_Needs_Approval_The_Run_Ends_With_Its_Request_And_It_Does_Not_Run`, `..._Given_An_Approval_The_Next_Run_Of_The_Conversation_Runs_The_Call`, `..._Given_A_Rejection_The_Model_Receives_Its_Reason_And_The_Call_Does_Not_Run`, `..._Given_A_Streaming_Run_The_Host_Receives_The_Request_And_Its_Approval_Runs_The_Call` |
| new | The stored request is the call that runs; stray and second answers change nothing (D24) | `ToolApprovalTests.Given_An_Answer_Carrying_Other_Arguments_The_Call_The_Conversation_Stored_Runs`, `..._Given_An_Answer_To_No_Stored_Request_Nothing_Runs`, `..._Given_A_Second_Answer_To_A_Request_The_First_Stands`, `..._Given_An_Answered_Approval_Later_Runs_Send_The_Call_Once_With_Its_Result` |
| new | Calls next to one that requires approval wait with it; without a conversation a run that asks fails | `ToolApprovalTests.Given_A_Call_Needing_Approval_Next_To_One_That_Does_Not_Only_The_First_Waits_And_Both_Run`, `..._Given_No_Conversation_A_Call_Needing_Approval_Fails_Naming_The_Tool` |
| new | Standing approvals and rules decide before the host; a rejection wins (D24) | `ToolApprovalTests.Given_An_Approval_For_The_Conversation_A_Later_Call_Of_The_Tool_Runs_Without_Asking`, `..._Given_A_Rule_That_Approves_The_Call_Runs_Without_Asking`, `..._Given_A_Rule_That_Rejects_The_Model_Receives_Its_Reason`, `..._Given_One_Rule_That_Approves_And_One_That_Rejects_The_Rejection_Wins`, `..._Given_A_Call_Needing_Approval_A_Rule_Sees_It_With_The_Runs_Mode`, `..._Given_A_Rule_Answering_One_Call_And_The_Host_Another_Both_Run_Once_The_Host_Answered`, `..._Given_A_Streaming_Run_And_A_Rule_That_Approves_No_Request_Reaches_The_Host` |
| new | Auto mode runs what requires approval unless a rule rejects it (D52) | `ModeTests.Given_Auto_Mode_A_Call_Needing_Approval_Runs_Without_Asking`, `..._Given_Auto_Mode_A_Rule_That_Rejects_Still_Wins`, `..._Given_Auto_Mode_The_Calls_It_Approves_Count_Toward_The_Runs_Limit` |
| new | Plan mode offers the read-only tools and `exit_plan_mode`; the approved plan continues in the chosen mode (D52) | `ModeTests.Given_Plan_Mode_Only_The_Read_Only_Tools_And_Exit_Plan_Mode_Are_Offered`, `..._Given_Plan_Mode_The_Instructions_Ask_For_A_Plan`, `..._Given_Plan_Mode_Presenting_The_Plan_Ends_The_Run_With_A_Request_That_Carries_It`, `..._Given_An_Approved_Plan_The_Run_Continues_In_The_Chosen_Mode`, `..._Given_A_Rejected_Plan_The_Model_Receives_The_Feedback_And_Stays_In_Plan_Mode` |
| new | A conversation keeps its mode unless a run names one (D52) | `ModeTests.Given_A_Conversation_A_Run_Without_A_Mode_Continues_In_The_Mode_The_Last_Run_Ended_In`, `..._Given_A_Run_Naming_A_Mode_It_Leaves_The_Conversations_Mode` |
| new | Run middleware wraps runs in order, may change the messages, and may answer in place of the model (D30) | `RunMiddlewareTests` |
| new | Streaming runs pass through run middleware, which reads the response once the stream ended (D30) | `RunMiddlewareTests.Given_A_Streaming_Run_The_Middleware_Reads_The_Response_Once_The_Stream_Ended`, `..._Given_A_Streaming_Run_Middleware_That_Answers_Is_Streamed`, `..._Given_A_Streaming_Run_Left_Early_The_Run_Ends_With_What_Arrived` |
| new | Tool middleware wraps calls in order and may answer in place of the tool (D30) | `ToolMiddlewareTests` |
| new | Context providers add instructions, unstored messages ahead of the input, and tools (D31) | `ContextProviderTests` |

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
- **Kinds across references:** TSH022 compares the compilation's agents with each other and with those the
  references stamp, not two references' agents with each other.
- **A tool loop a tool ends:** tool results after which the tool loop makes no model call (a function that sets
  `Terminate`) are not stored; handoff, which ends the loop this way, stores them in slice 8.
- **Live updates of non-streaming runs:** only streaming runs publish.
- **Storable structs:** a user struct other than the scalars cannot be configurable (TSH012).
- **Plan mode's other tools:** plan mode offers `ReadOnly()` toolset tools and `exit_plan_mode`; `write_todos`, MCP
  servers marked `ReadOnly()` and a sub-agent's inherited mode come with slices 8 and 9.
- **Run limit across approval rounds:** rounds the approval layer continues share the run's `MaxToolIterations`; a
  round that runs tools before it asks again may run one round of tools past it.
