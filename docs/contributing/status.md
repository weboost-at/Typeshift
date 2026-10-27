---
title: Status
description: "Where the library stands: the slices of the rewrite, the test that covers each behavior, and the known gaps."
---

# Status

What is built, which test covers each behavior, and what is still open. Open items live here, never as TODOs in
code.

## Slices

The rewrite built the API the owner approved in ten slices, one at a time, and all ten are done:

1. Toolsets and tools: generated functions, overrides, tool lists, the analyzer.
2. Agents, registration, `AgentHost` runs.
3. Settings store, id type.
4. Conversations: checkpoints, resume, live updates, steering.
5. Approval, rules, plan and auto modes.
6. Middleware, context providers.
7. Telemetry, compaction.
8. Sub-agents, handoff.
9. MCP, built-in toolsets.
10. Native AOT: the console sample, the package consumer.

## Behavior coverage

A number is the rule's row in v0's behavior table, where that rule still holds. Rows marked "new" came with the
rewrite.

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
| new | A failed tool call reaches the model with its exception's message (D53) | `AgentRunTests.Given_A_Failing_Tool_Call_The_Model_Reads_Why_It_Failed` |
| new | A run's tools resolve from the scope its host came from (D25) | `AgentRunTests.Given_Two_Tool_Calls_Of_A_Run_Both_Resolve_From_The_Hosts_Scope` |
| new | Messages and updates carry the agent's name (D26) | `AgentRunTests.Given_A_Run_Its_Messages_Carry_The_Agents_Name_As_Their_Author`, `..._Given_A_Streaming_Run_Its_Updates_Carry_The_Text_And_The_Agents_Name` |
| new | Run options limit the tool loop and may be configurable (D32) | `AgentRunTests.Given_Run_Settings_Of_A_Run_Option_The_Tool_Loop_Stops_After_It` |
| new | A run asks the chat client provider for the provider and model of code, stored settings or the run; the request names the model alone (D56) | `AgentRunTests.Given_Run_Settings_They_Override_The_Configurable_Options`, `..._Given_A_Provider_And_A_Model_The_Request_Names_Only_The_Model`, `..._Given_Options_Of_Its_Own_The_Created_Client_Asks_The_Agents_Provider`, `StoredSettingsTests.Given_Stored_Settings_A_Run_With_Their_Id_Applies_Them` |
| new | The provider id type types the alias, the `Provider` members and the core services (D56) | `AgentGenerationTests.Given_A_Provider_Id_Type_The_Alias_The_Settings_And_The_Core_Services_Use_It` |
| new | Structured output and the raw path | `AgentRunTests.Given_A_Result_Type_The_Run_Returns_The_Models_Json_Typed`, `..._Given_Resolved_Options_The_Created_Client_Runs_The_Agent` |
| new | A run without a model or a provider fails, saying how to fix it | `AgentRunTests.Given_No_Model_The_Run_Fails_Saying_How_To_Set_One`, `AddTypeshiftTests.Given_No_Chat_Client_Provider_A_Run_Fails_Naming_The_Registration` |
| new | Disposing a run's client leaves the provider's client alone | `AgentRunTests.Given_A_Run_The_Providers_Client_Outlives_It` |
| new | The XML docs of a parameter's type describe its properties; the module registers a marked class's docs (D55) | `AddTypeshiftTests.Given_Xml_Docs_On_A_Parameters_Type_Its_Schema_Describes_Its_Properties`, `ToolsetGenerationTests.Given_A_Schema_Annotations_Class_The_Module_Registers_Its_Docs`, `..._Given_A_Schema_Annotations_Class_That_Is_Not_Partial_The_Module_Leaves_It_Out` |
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
| new | Run watchers hear the runs going on, then each start and end (D54) | `ConversationRunTests.Given_A_Run_Watcher_It_Hears_The_Run_Start_And_End`, `..._Given_A_Run_Going_On_A_Run_Watcher_Hears_It_Once_Then_Its_End` |
| new | Watchers receive streamed updates (D47) | `ConversationRunTests.Given_A_Watcher_It_Receives_The_Updates_Of_A_Streaming_Run` |
| new | Text streamed within the publish interval reaches watchers merged, ahead of the next call (D47) | `ConversationRunTests.Given_Text_Within_The_Interval_A_Watcher_Receives_It_Merged_Ahead_Of_The_Next_Call` |
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
| new | A run is an `invoke_agent` span around its chat and tool spans, with usage and errors (D36) | `TelemetryTests` |
| new | Compaction clears the oldest read results, then summarizes the oldest turns, by calibrated estimate (D32) | `CompactionTests` |
| new | The summary runs on the run's provider and model unless the agent or the settings name others (D56) | `CompactionTests.Given_No_Summary_Model_The_Runs_Model_Summarizes`, `..._Given_Clearing_Is_Not_Enough_The_Oldest_Turns_Are_Summarized`, `..._Given_A_Summary_Provider_It_Serves_The_Summary_Model` |
| new | A summary is stored, and later runs start from it (D32) | `CompactionTests.Given_A_Summary_Later_Runs_Start_From_It` |
| new | Summarizing keeps the latest `write_todos` call with its result (D32, D37) | `CompactionTests.Given_Summarized_Turns_The_Latest_Todo_List_Stays_With_Its_Result` |
| new | A sub-agent is a tool of its description and one input, answering in a fresh context in its caller's mode (D33) | `SubAgentTests` |
| new | A sub-agent's calls that need approval are rejected unless something approves them (D33) | `SubAgentTests.Given_A_Sub_Agent_In_The_Default_Mode_A_Call_That_Needs_Approval_Is_Rejected` |
| new | A sub-agent inherits the standing approvals of its caller's conversation (D24, D33) | `SubAgentTests.Given_A_Tool_Approved_For_The_Callers_Conversation_The_Sub_Agents_Call_Of_It_Runs` |
| new | A background sub-agent's answer joins the run, which waits for it; a lost one is reported on resume (D33) | `SubAgentTests.Given_A_Background_Sub_Agent_The_Caller_Works_On_And_Receives_Its_Answer_Before_It_Ends`, `..._Given_An_Interrupted_Run_With_A_Background_Sub_Agent_The_Model_Hears_It_Was_Lost` |
| new | A handoff continues the run with the other agent, which answers the conversation's later runs (D34) | `HandoffTests` |
| new | Sub-agents and handoff targets are checked at compile time (TSH023, TSH024, TSH026) | `DiagnosticTests` |
| new | MCP servers' tools follow the agent's own, read-only or behind approval; a clash or no provider fails (D35) | `McpTests` |
| new | The file tools stay within the root, links included, and read, list, search, write, edit and delete (D37) | `FileSystemToolsetTests` |
| new | `run_command` runs in the root, with a timeout and an output limit (D37) | `ShellToolsetTests` |
| new | `run_command` reads a command's output to its end after the timeout kills it, and a second past it for what a process left behind writes (D37) | `ShellToolsetTests.Given_A_Process_Left_Behind_What_It_Writes_Just_After_The_Timeout_Is_Kept`, `..._Given_A_Command_That_Leaves_A_Process_Behind_It_Returns_At_The_Timeout` |
| new | `delete_file` and `run_command` ask first; the three file readers are read-only (D37) | `FileSystemToolsetTests.Given_The_File_And_Shell_Tools_Those_That_Change_Things_Ask_And_The_Readers_Only_Read` |
| new | The web tools refuse other schemes and name a missing registration (D37) | `WebToolsetTests` |
| new | A consumer's agent configures a built-in tool across the assembly boundary (D7, D37) | `FileSystemToolsetTests.Given_An_Agent_Using_The_Built_In_Toolset_Its_Configuration_Reaches_The_Tool` |
| new | A reference without agents has no id type to clash (D11) | `DiagnosticTests.Given_A_Reference_Of_Toolsets_Only_Built_With_Another_Id_Type_TSH021_Is_Not_Reported` |
| new | Under Native AOT, a tool type no context lists is TSH027 (D45) | `DiagnosticTests.Given_A_Tool_Type_No_Context_Lists_Under_Native_Aot_TSH027_Is_Reported_On_Its_Tool` |
| new | Tools, settings, conversations, streaming and structured output run published as Native AOT | `samples/Typeshift.Samples.Console`, run by CI |
| new | A consumer of the package, not of the projects, generates and registers its agents and the built-ins | `samples/Typeshift.Samples.PackageConsumer`, run by CI |

## Known gaps

- **Recursive parameter types.** Each parameter's schema is created on its own and nested under the function's
  `properties`, so a `$ref` that `AIJsonUtilities.CreateJsonSchema` emits for a recursive type is relative to the
  parameter. This is untested, and non-recursive types are unaffected.
- **Tool names across references.** TSH007 compares the compilation's tools with each other and with the tools the
  references stamp, but not two references' tools with each other. An agent offering two referenced tools of one
  name keeps the first, and the generated code stays valid.
- **Provider id types across references.** The module stamps carry the id type but not the provider id type, so a
  referenced project built with another `TypeshiftProviderIdType` compiles, and a run of its agents fails with an
  `InvalidOperationException` that names both types.
- **A public agent with a less accessible provider id type.** A configurable model puts the provider id type into the
  agent's public `Settings`, so an internal enum fails with CS0053 instead of a Typeshift diagnostic.
- **Generated line length.** Generated code is `global::`-qualified, and some declarations exceed 120 columns.
- **Streamed results.** A tool returning `IAsyncEnumerable<T>` compiles, but its result is serialized synchronously
  and the call fails at runtime. Neither v0 nor the approved API supports it.
- **Kinds across references.** TSH022 compares the compilation's agents with each other and with those the
  references stamp, but not two references' agents with each other.
- **Live updates of non-streaming runs.** Only streaming runs publish.
- **Docs of types in assemblies without toolsets or agents.** Such an assembly has no module, so the XML docs
  collected for it reach no schema. A tool whose parameter type lives there gets that type's properties undescribed.
- **Storable structs.** A user struct other than the scalars cannot be configurable (TSH012).
- **Run limit across approval rounds.** The rounds the approval layer continues share the run's
  `MaxToolIterations`, so a round that runs tools before it asks again may run one round of tools past it.
- **Compaction's estimate.** About four characters make a token, corrected by the provider's report of the run's
  last call, so a run's first call trusts the estimate alone. The summary model reads results cut to 2,000
  characters.
- **Compaction across steering.** A steered run's later pass compacts afresh and summarizes again if it needs to.
- **Agents across references.** TSH023, TSH024 and TSH026 check the agents of the same compilation. A sub-agent of a
  referenced assembly is checked when that assembly builds, not against its callers.
- **File tools and links.** The root check follows every link on a path, but a link swapped in between the check
  and the use still escapes, and only a sandbox stops that. `edit_file` holds a file whole in memory and refuses
  files over 16 MB. Paths compare as the operating system opens them, which on Windows drops a last segment's
  trailing dots and spaces; CI runs on Linux, so that is untested.
- **Sub-agents in plan mode.** A sub-agent's tool carries no `ReadOnly()` mark, so plan mode hides every sub-agent,
  even one that only reads. A sub-agent run in plan mode would be asked for a plan it has no host to present to.
- **The raw path's client under concurrent calls.** The client `CreateChatClientAsync` returns holds one tool loop,
  whose iteration limit the approval layer sets per call, so the client serves one call at a time.
- **A conversation loads whole.** `LoadConversationAsync` returns every entry of a conversation, and a run reads all
  of them although its history starts at the last summary; standing approvals and lost background agents are read
  from the whole conversation too.
- **A provider client per call.** A provider that returns a new client instance per call gets a new instrumented
  wrapper per call, with a meter that lives as long as the instance. Return one instance per model.
- **`ConfigureAsync` under a store that always conflicts.** Each conflict is read as another save that won, so the
  retry has no cap and ends only when the token is cancelled.
- **Web fetch and private addresses.** The model picks the URL, and the host's `IWebFetch` decides which addresses
  it reaches. No fetcher ships with Typeshift.
- **Shell.** `run_command` runs what the model sends. The root is its working directory but not a sandbox, and the
  tool requires approval unless a rule or auto mode answers.

## Related

- [Decisions](./decisions.md): why the built behavior is shaped the way it is.
- [Architecture](./architecture.md): where each behavior lives in the code.
