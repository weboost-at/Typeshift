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

        WithRunOptions()
            .WithMaxToolIterations(500)
            .WithMaxInputTokens(150_000)
            .WithSummaryModel("fast-model")
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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Agents/CodingAgent.cs#L5-L36' title='Snippet source file'>snippet source</a> | <a href='#snippet-coding-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Run options

`WithRunOptions()` sets how a run goes:

<!-- snippet: long-running-options -->
<a id='snippet-long-running-options'></a>
```cs
WithRunOptions()
    .WithMaxToolIterations(500)
    .WithMaxInputTokens(150_000)
    .WithSummaryModel("fast-model")
    .Configurable(r => r.MaxToolIterations);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Agents/CodingAgent.cs#L17-L23' title='Snippet source file'>snippet source</a> | <a href='#snippet-long-running-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

| Option | Default | Does |
|---|---|---|
| `WithMaxToolIterations` | 40 | The most model calls one run's tool loop makes. |
| `WithMaxConsecutiveToolErrors` | 3 | The most failed tool calls in a row before the run ends. |
| `WithConcurrentToolCalls` | off | Runs the tool calls of one model response at the same time. |
| `WithMaxInputTokens` | off | Turns compaction on. |
| `WithTargetInputTokens` | half the maximum | How far compaction shrinks the history. |
| `WithSummaryModel` | none | Summarizes what clearing tool results did not free. |

`Configurable(r => r.MaxToolIterations)` puts an option into the agent's `Settings`, like a chat option, so stored
settings or a run may change it.

## Compaction

Before each model call, also between the calls of one run, Typeshift checks the input tokens the provider reported
for the last call. When they reached `WithMaxInputTokens`, it shrinks the history to `WithTargetInputTokens`:

1. It clears the results of the oldest tool calls, keeping the calls, so the model knows what it did.
2. If that is not enough, it summarizes the oldest turns with the summary model, or drops them without one.

The instructions, the latest `write_todos` call and a tool call with its result always stay together. The model
sees the compacted history; the stored conversation keeps everything, and a summary is stored as an entry of its own,
so later runs start from it instead of summarizing again.

## Checkpoints

A run is stored as it goes: its input, every model response and every tool result, each the moment it exists. A host
that crashes mid-run loses at most the call in flight. Tools with side effects should be safe to call twice, since
a call interrupted while it ran is left out when the run continues.

<!-- snippet: long-running-run -->
<a id='snippet-long-running-run'></a>
```cs
var coding = scope.ServiceProvider.GetRequiredService<AgentHost<CodingAgent>>();
var conversationId = 12;

await foreach (var update in coding.RunStreamingAsync("Move the solution to .NET 10.", settingsId, conversationId))
    Console.Write(update.Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/LongRunning.cs#L13-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-long-running-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Resuming

`ResumeAsync` continues a conversation's interrupted run from its last stored step, without new input:

<!-- snippet: resume -->
<a id='snippet-resume'></a>
```cs
var resumed = await coding.ResumeAsync(conversationId, settingsId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/LongRunning.cs#L21-L23' title='Snippet source file'>snippet source</a> | <a href='#snippet-resume' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A new run on that conversation works too: it starts from the interrupted run's steps, followed by its own input.
Typeshift runs one run per conversation at a time in a process; across processes, the host makes sure of it.

## Approval without interruptions

A long run stops whenever it needs approval. Two ways keep it going: the host approves a tool for the rest of the
conversation, or a rule approves the calls it trusts; see
[tool approval](./tool-approval.md#approving-for-the-conversation).

## Watching

`RunStreamingAsync` streams the run to the caller, and
[`ConversationTracker.WatchAsync`](./conversations.md#live-updates) to anyone else watching, a UI in another
request say. Each model call is traced as its own span; see [observability](./observability.md).
