# Long-running agents

Most runs take a few model calls. An agent that works through a task on its own, a coding agent say, runs hundreds
of calls in one run, for an hour or more. Typeshift keeps such a run within its context, stores it as it goes and
lets the host stop asking for every command.

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

`WithRunOptions()` sets how a run goes:

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

| Option | Default | Does |
|---|---|---|
| `WithMode` | `Default` | The [mode](./modes.md) runs start in: `Default`, `Plan` or `Auto`. |
| `WithMaxToolIterations` | 40 | The most rounds of tool calls one run makes. |
| `WithMaxConsecutiveToolErrors` | 3 | The most failed tool calls in a row before the run ends. |
| `WithConcurrentToolCalls` | off | Runs the tool calls of one model response at the same time. |
| `WithMaxInputTokens` | off | Turns compaction on. |
| `WithTargetInputTokens` | half the maximum | How far compaction shrinks the history. |
| `WithSummaryModel` | none | Summarizes what clearing tool results did not free. |

`Configurable(r => r.MaxToolIterations)` puts an option into the agent's `Settings`, like a chat option, so stored
settings or a run may change it.

## Compaction

Before each model call, also between the calls of one run, Typeshift estimates the call's input tokens: about four
characters to a token, corrected by the tokens the provider reported for the run's last call. When the estimate
reaches `WithMaxInputTokens`, it shrinks the history the model sees to `WithTargetInputTokens`:

1. It clears the results of the oldest tool calls the model has already read, keeping the calls, so the model knows
   what it did and can call again.
2. If that is not enough, it summarizes the oldest turns with the summary model, or drops them without one.

The instructions, the latest `write_todos` call and a tool call with its result always stay together. Later calls of
the run see the same compaction. The stored conversation keeps everything, and what compaction left, the summary
and the messages after it, is stored as an entry of its own, so later runs start from it instead of summarizing
again.

## Checkpoints

A run is stored as it goes: its input, every model response and every tool result, each the moment it exists. A host
that crashes mid-run loses at most the call in flight. Tools with side effects should be safe to call twice, since
a call interrupted while it ran is left out when the run continues.

## Resuming

`ResumeAsync` continues a conversation's interrupted run from its last stored step, without new input:

<!-- snippet: resume -->
<a id='snippet-resume'></a>
```cs
var resumed = await support.ResumeAsync(interrupted, settingsId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L60-L62' title='Snippet source file'>snippet source</a> | <a href='#snippet-resume' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A new run on that conversation works too: it starts from the interrupted run's steps, followed by its own input.
Typeshift runs one run per conversation at a time in a process; across processes, the host makes sure of it.

## Steering

A user can steer a run while it works: `TrySteer` hands input to the run of the conversation going on in this
process. The model receives it before its next call, the run does not end before it has, and it is stored with the
conversation like any input. With no run going on here, start a run with the input instead:

<!-- snippet: steering -->
<a id='snippet-steering'></a>
```cs
var input = "Send me the invoice too.";

if (!tracker.TrySteer(conversationId, input))
    await support.RunAsync(input, settingsId, conversationId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L48-L53' title='Snippet source file'>snippet source</a> | <a href='#snippet-steering' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

To stop a run instead, cancel its token; it is stored up to where it stopped.

## Sub-agents

A long run keeps its context small by handing searches and side quests to [sub-agents](./multi-agent.md#sub-agents),
which answer in a paragraph; in the background, they work while the run goes on.

## Approval without interruptions

A long run stops whenever it needs approval. Three ways keep it going: the host approves a tool for the rest of the
conversation, a rule decides the calls it is sure about
([tool approval](./tool-approval.md#approving-for-the-conversation)), or the run is in
[auto mode](./modes.md#auto-mode), where the rules only guard. [Plan mode](./modes.md#plan-mode) lets the host approve
the whole plan once instead of each step.

## Watching

`RunStreamingAsync` streams the run to the caller, and
[`ConversationTracker.WatchAsync`](./conversations.md#live-updates) to anyone else watching, a UI in another
request say. Each model call is traced as its own span; see [observability](./observability.md).
