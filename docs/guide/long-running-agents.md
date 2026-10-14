---
title: Long-running agents
description: Run an agent for hundreds of model calls, with compaction, crash recovery, sub-agents and fewer approvals.
---

# Long-running agents

Most runs take a few model calls. An agent that works through a task on its own, such as a coding agent, makes hundreds
of calls in one run, for an hour or more. Typeshift keeps such a run within its context, stores it as it goes and lets
the host stop approving every command.

::: tip The short version

- Raise `WithMaxToolIterations` above its default of 40, and turn compaction on with `WithMaxInputTokens`.
- Compaction clears old tool results first, then summarizes or drops the oldest turns. The stored conversation keeps
  everything.
- Every step is stored as it happens, so a crash loses at most the call in flight. `ResumeAsync` continues the run.
- Sub-agents keep the run's context small. Standing approvals, rules and auto mode keep it from stopping for approval.

:::

## A coding agent

This agent plans with to-dos, works on files, runs shell commands and fetches web pages. It hands exploring the
workspace to a sub-agent in the background, and starts in plan mode:

<!-- snippet: coding-agent -->
<a id='snippet-coding-agent'></a>
```cs
internal partial class CodingAgent : Agent
{
    public CodingAgent()
    {
        WithChatOptions().WithModel("reasoning-model");

        UseToolset<TodoToolset>();
        UseToolset<FileSystemToolset>();
        UseToolset<ShellToolset>();
        UseToolset<WebToolset>().Include(t => t.WebFetch);

        UseAgent<ExploreAgent>().InBackground();

        WithRunOptions()
            .WithMode(AgentMode.Plan)
            .WithMaxToolIterations(500)
            .WithMaxInputTokens(150_000)
            .WithSummaryModel("fast-model")
            .Configurable(r => r.Mode)
            .Configurable(r => r.MaxToolIterations);

        ConfigureRunCommand()
            .WithShell("/bin/bash")
            .WithTimeout(TimeSpan.FromMinutes(2));
    }

    protected override string GetInstructions() =>
        """
        Work in the customer's workspace. Plan with write_todos, read before you edit, and run the tests after
        every change.
        """;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/CodingAgent.cs#L5-L42' title='Snippet source file'>snippet source</a> | <a href='#snippet-coding-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Run options

`WithRunOptions()` sets how a run's tool loop goes:

<!-- snippet: long-running-options -->
<a id='snippet-long-running-options'></a>
```cs
WithRunOptions()
    .WithMode(AgentMode.Plan)
    .WithMaxToolIterations(500)
    .WithMaxInputTokens(150_000)
    .WithSummaryModel("fast-model")
    .Configurable(r => r.Mode)
    .Configurable(r => r.MaxToolIterations);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/CodingAgent.cs#L21-L29' title='Snippet source file'>snippet source</a> | <a href='#snippet-long-running-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- **`WithMaxToolIterations(500)`** raises the most rounds of tool calls one run makes, 40 unless set.
- **`WithMaxInputTokens(150_000)`** turns [compaction](#compaction) on at that many input tokens.
- **`WithSummaryModel("fast-model")`** names the model that summarizes what clearing tool results did not free.
- **`WithMode(AgentMode.Plan)`** starts runs in [plan mode](./modes.md#plan-mode).
- **`Configurable(r => r.MaxToolIterations)`** puts an option into the agent's `Settings`, like a chat option, so stored
  settings or a run may change it.

[Builders](./builders.md#runoptionsbuilder) lists every run option with its default, such as the limit on failed tool
calls in a row and running a response's tool calls at the same time.

## Compaction

Compaction shrinks the history a model call sees, so a long run stays within the model's context. It is off until
`WithMaxInputTokens` sets a limit.

Before every model call, not only the first of a run, Typeshift estimates the call's input tokens. It counts about four
characters to a token, corrected by the tokens the provider reported for the run's last call. When the estimate reaches
`WithMaxInputTokens`, Typeshift shrinks the history the model sees to `WithTargetInputTokens`, half the maximum unless
set:

1. It clears the results of the oldest tool calls the model has already read. It keeps the calls, so the model knows
   what it did and can call again.
2. If that is not enough, it summarizes the oldest turns with the summary model, or drops them without one.

The instructions and the latest `write_todos` call always stay, and a tool call always stays together with its result.
Later calls of the run see the same compaction.

The stored conversation keeps everything. Compaction also stores what it left, the summary and the messages after it, as
an entry of its own. Later runs start from that entry instead of summarizing again.

## Crashes and steering

A run is stored step by step, so a host that crashes mid-run loses at most the call in flight. `ResumeAsync` continues
the run from its last stored step, and the model may call an interrupted tool again; see
[resuming](./conversations.md#resuming). While the run works, a user can [steer](./conversations.md#steering) it with
new input.

## Sub-agents

A long run keeps its context small by handing searches and side quests to [sub-agents](./multi-agent.md#sub-agents),
which answer in a paragraph. [In the background](./multi-agent.md#in-the-background), they work while the run goes on.

## Approval without interruptions

A long run stops whenever it needs approval. Three ways keep it going:

- The host [approves a tool for the rest of the conversation](./tool-approval.md#approving-for-the-conversation).
- An [approval rule](./tool-approval.md#approval-rules) decides the calls it is sure about.
- The run is in [auto mode](./modes.md#auto-mode), where the rules only guard.

[Plan mode](./modes.md#plan-mode) lets the host approve the whole plan once instead of each step.

## Watching

`RunStreamingAsync` streams the run to its caller. [`ConversationTracker.WatchAsync`](./conversations.md#live-updates)
streams it to anyone else watching, such as a UI in another request. Each model call is traced as its own span; see
[Observability](./observability.md).

## Related

- [Conversations](./conversations.md): storing, resuming and steering a run.
- [Builders](./builders.md#runoptionsbuilder): every run option and its default.
- [Multiple agents](./multi-agent.md#sub-agents): sub-agents, in the background or not.
- [Plan and auto modes](./modes.md): fewer approvals in a long run.
- [Built-in toolsets](./built-in-toolsets.md): the to-dos, files, shell and web tools a coding agent uses.
