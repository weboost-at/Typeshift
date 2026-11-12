---
title: How a run works
description: The steps every run passes through, in order, and what your middleware, context providers, approval rules and tool middleware see and can change at each one.
---

# How a run works

A run is one call of `RunAsync`, `RunAsync<T>`, `RunStreamingAsync`, `ResumeAsync` or `ResumeStreamingAsync`, or of a
client from the [raw path](./running-agents.md#the-raw-path). Streaming or not, every run passes through the same
steps in the same order, and that order tells you what your middleware sees, when context is added, which calls ask
for approval and what is stored when.

```mermaid
flowchart TD
  start(["RunAsync(input, settingsId, conversationId, settings)"]) --> resolve[Resolve the options]
  resolve --> conversation[Resume the conversation]
  conversation --> middleware[Run middleware]
  middleware --> context[Context providers]
  context --> approval[Approval and mode]
  approval --> before
  subgraph loop [Tool loop]
    before[Store new steps, then compact] --> model[Call the model]
    model -->|tool calls| tools[Tool middleware, then the tools]
    tools --> before
  end
  model -->|an answer| done([ChatResponse])
```

Each step wraps the steps below it, so the answer passes back up through them in reverse order: run middleware sees
the response before the conversation stores how the run ended.

## The steps

1. **Resolve the options.** The host builds the run's `ChatOptions` from the agent's code, then the settings stored
   under the settings id, then the run's own settings. It creates the tools the agent offers, their settings resolved
   through the same layers, and adds the sub-agents and the tools of MCP servers after them. The layers resolve once,
   when the run starts ([settings layering](./settings-and-stores.md#settings-layering)).
2. **Resume the conversation.** With a conversation id, the run puts the stored entries ahead of the new input, so the
   model sees the whole conversation, and stores the input. An input that answers an approval request that has
   closed throws instead, before anything is stored
   ([When a request closes](./tool-approval.md#when-a-request-closes)). The run keeps the conversation's mode unless
   the run's settings name one. If an earlier run [handed the conversation off](./multi-agent.md#handoff) to another
   agent, the agent that answered last answers this run too. Without a conversation id, the run starts fresh and
   stores nothing. Within one process, a conversation has one run at a time, and starting a second while the first
   goes on throws ([Conversations](./conversations.md)).
3. **Run middleware.** Each `IRunMiddleware` wraps the rest of the run ([Middleware](./middleware.md)).
4. **Context providers.** Each context provider the agent uses can add instructions, messages and tools to this run.
   The conversation stores none of it, so every run calls the providers again
   ([Context providers](./middleware.md#context-providers)).
5. **Approval and mode.** If the input carries your app's answers to the approval requests the conversation's last
   run left open, the run matches each answer to its request. In plan mode, it offers the model only the read-only
   tools and `exit_plan_mode`. After each pass of the tool loop, approval rules, standing approvals and auto mode
   decide every call they can. If calls remain that only your app can decide, the run stores their requests and ends
   ([Approval and modes](./tool-approval.md)).
6. **The tool loop.** Microsoft.Extensions.AI's `FunctionInvokingChatClient` calls the model, runs the tools it asks
   for, each through the tool middleware, and calls the model again until it answers. The
   [run options](./defining-agents.md#run-options) set its limits, such as the most rounds of tool calls.

## Inside the tool loop

Before every model call inside the loop, the run appends the new steps to the conversation: tool results, input your
app [steered](./conversations.md#steering) into the run, and the answers of sub-agents running
[in the background](./multi-agent.md#in-the-background). Each model response is stored as it arrives, so a crash
loses at most the calls in flight.

When compaction is on and a call would exceed the token limit, the run compacts what the model sees, while the stored
conversation keeps everything ([compaction](./conversations.md#compaction)). Typeshift traces every call to the
provider's client ([Observability](./production.md#observability)).

## What each step can change

| Step | Sees | Can |
|---|---|---|
| Run middleware | The agent, the messages, the options and the mode | Change the messages and options of this run, or answer in place of the model |
| Context provider | The run | Add instructions, messages and tools to this run |
| Approval rule | Each call that requires approval, with the arguments the model sent | Approve it, reject it with a reason for the model, or abstain |
| Tool middleware | Each tool call: the function, its arguments, the messages so far | Answer in place of the tool, or end the tool loop |

## Related

- [Running an agent](./running-agents.md): the calls that start a run, and the raw path.
- [Middleware and context](./middleware.md): wrapping every run and every tool call, and adding context.
- [Approval and modes](./tool-approval.md): what happens when a call needs approval.
- [Conversations](./conversations.md): storing, resuming and steering runs.
- [How Typeshift reads your code](./how-typeshift-reads-your-code.md): what the build writes before any run.
- [Architecture](../contributing/architecture.md#a-run): the same pipeline from the code's side.
