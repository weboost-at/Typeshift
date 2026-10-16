---
title: Retrospective
description: "What building three hosts on the library showed: choosing a model from a provider at runtime, the other friction, and the features to add before v1."
---

# Retrospective

Three demo hosts were built on the current library to use every feature as an application would: an app whose users
define their own model providers at runtime, a long-running coding agent, and a support desk of four agents. This
page records where the library got in the way, proposes what to change, and lists what to add before v1. The API
sketches are plain text blocks, since they describe code that does not exist yet.

## What the demos showed

The library holds up. The three hosts under `demos/` run every feature offline on canned models: handoffs with an
approval in the middle and a hand-back, foreground and background sub-agents, plan mode approved into auto mode,
rules, tool and run middleware, context providers, settings per customer with tool overrides, an MCP server,
compaction, steering while a run works, a conversation on disk that resumes, the live list of runs, typed output
and the raw path. Nothing in the runtime had to be worked around beyond what this page lists, and the audit's
fixes came from reading, not from the demos breaking.

What stood out is the code every host writes around the library, the same in all three: a provider that maps
model names to clients, a cache for those clients, a store, and a loop that turns approval requests into the next
run's answers. `Typeshift.Demos.Shared` holds that code once; the proposals below move most of it into the
library.

Live runs went through the local LM Studio server with a small model, `qwen/qwen3-vl-4b`, once the server came
back from a crash while loading a 27B one. The plumbing held: the composite name routed to the server, the response
named the wire model, tools were called and answered, streaming merged, the handoff and the hand-back ran, the MCP
tool answered and the typed JSON summary parsed. The model's judgment did not: it asked the customer whether to
refund instead of calling `issue_refund`, so nothing waited for approval, and in the coding demo it looped in text
for 6,000 tokens in plan mode without presenting a plan. Two things for the proposals came out of that. LM Studio
served `lmstudio/does-not-exist` with whatever model it had loaded, so without a catalog a typo is not even an
error. And a run needs `WithMaxOutputTokens` as a guard against a looping model, which a host should set and the
guide should say.

## Choosing a model from a provider at runtime

The multi-provider demo is the app from the grudge: its users define providers in a file the app edits at runtime,
an OpenAI-compatible endpoint, Anthropic, a canned one for tests, and each user picks a model. Six things stand
between that app and the library, each marked `CLUNKY n` in the demo's code:

1. **One string names a model.** `IChatClientProvider.GetChatClientAsync(string model)` and `WithModel` know a
   model as one name, so the app invents a composite, `lmstudio/qwen/qwen3.8-27b`, and a delimiter that must not
   collide with model ids, which carry slashes of their own. Every host reinvents the parsing.
2. **The code must name a model.** An agent without `WithModel` fails at run time, so the demo's agents name
   `default`, a sentinel its provider resolves to the user's choice. The sentinel is a convention the library knows
   nothing about.
3. **No model catalog.** A settings UI needs the models it may offer, and a stored `Model` is any string. The app
   asks each provider itself, and only OpenAI-compatible servers can answer.
4. **The provider is scoped, the clients are not.** `IChatClientProvider` is scoped so it can use the request's
   credentials, but clients must live for the process, and Typeshift now instruments each instance once. So the
   host writes a second singleton to hold them and a provider that is a thin shell over it.
5. **Nothing validates a stored model.** `ConfigureAsync` stores any string, and a typo surfaces in the middle of
   a run, from the provider's exception.
6. **`ChatOptions.ModelId` reaches the wire.** The provider's client sends `ModelId` as the request's model, and
   Typeshift puts the app's composite name there, which no server knows. Every client has to be wrapped to put the
   real id back, and the spans still show the composite name as `gen_ai.request.model`.

### Proposal: the provider returns the client and the wire model

`IChatClientProvider` takes a request that says what the run knows, and returns the client together with the model
id to send:

```text
public interface IChatClientProvider
{
    ValueTask<ChatClientLease> GetChatClientAsync(ChatClientRequest request, CancellationToken ct);
}

// What the run knows when it asks: the name the agent's options or settings hold, null when the code names
// none, the agent, the settings id, and whether the client answers the run or summarizes for compaction
public sealed record ChatClientRequest(string? Model, Agent Agent, object? SettingsId, ChatClientPurpose Purpose);

// The client, and the model id Typeshift puts into ChatOptions.ModelId before each call; null clears it, so
// the client's own default model applies
public sealed record ChatClientLease(IChatClient Client, string? ModelId = null);
```

This removes 1 and 6 at the root: the name stays the host's routing key, the wire model is the provider's answer,
and the wrapper goes away. The spans carry the wire model as `gen_ai.request.model` and the host's name as a tag of
the `invoke_agent` span. The request's agent and settings id let a provider apply policy, such as a cheaper model
for one agent or a tenant's own endpoint. The summary model of compaction goes through the same call with
`Purpose = Summary`.

This is a breaking change to a one-method interface, which is why it belongs before v1. A default interface member
could keep the string overload alive, but two methods on a one-method interface are a trap for the next reader;
the migration is one line per provider.

### Proposal: the code may name no model

`WithModel` becomes optional. A run whose layers resolve no model asks the provider with `Model = null`, and the
provider decides, per tenant or per agent. A provider that returns nothing fails the run naming the agent, as a
missing model does today. With `Configurable(o => o.ModelId)` the user's choice still wins. The sentinel from 2
goes away.

### Proposal: a model catalog

```text
public interface IModelCatalog
{
    ValueTask<IReadOnlyList<ModelInfo>> ListAsync(CancellationToken ct);
    ValueTask<bool> ExistsAsync(string model, CancellationToken ct);
}

public sealed record ModelInfo(string Name, string? DisplayName = null, string? Provider = null);
```

Registered with `WithModelCatalog<T>()`, scoped like the provider. A settings UI lists what it offers through one
Typeshift type instead of asking providers. When a catalog is registered, `ConfigureAsync` and `SaveSettingsAsync`
refuse a `Model` or `SummaryModel` the catalog does not know, with an `ArgumentException` that names it, so a
typo surfaces where it is typed and not in a run. That covers 3 and 5.

### Proposal: a provider base with the cache and the lifetime

Most hosts that let users define providers need the same thing: parse the name, build a client per provider and
model, keep it, dispose it at the end. The library can ship it, so a host implements one method:

```text
public abstract class RoutingChatClientProvider : IChatClientProvider
{
    // Builds the client of one provider and model; the base keeps it for the process, keyed by both, and
    // disposes it with the container
    protected abstract ValueTask<IChatClient> CreateClientAsync(string provider, string model, CancellationToken ct);

    // "lmstudio/qwen/qwen3.8-27b": the provider up to the first slash, the model after it; overridable
    protected virtual (string Provider, string Model) Parse(string name) => ...;
}
```

The base's cache is a singleton the library registers, so the provider itself stays scoped for credentials. That
covers 4, and with the lease it returns the model part as the wire model.

### Proposal: the names in telemetry

With the lease, `gen_ai.request.model` on the `chat` spans is the wire model, as the semantic conventions mean it,
and the `invoke_agent` span gains `typeshift.model`, the name the host chose. Dashboards can group by either.

## Other friction and proposals

Numbered to cite them, in the order the demos met them.

1. **A library cannot call `AddTypeshift()` for its hosts.** The call throws unless the calling project's generator
   intercepted it (D1), and a shared hosting library has no toolsets, so no generator runs for it. The guard is
   right; what is missing is a way to extend the chain. Proposal: `TypeshiftBuilder.Services`, the service
   collection the chain registers into, so a library offers `WithMyHost(this TypeshiftBuilder builder)` and the
   host keeps the one intercepted call.
2. **`WithStore<TStore>()` takes a type the container constructs.** A store that takes a directory or a connection
   string needs a registration of its own, and the chain's method does nothing for it. Proposal: overloads that
   take an instance or a factory, like the rest of dependency injection:
   `WithStore(Func<IServiceProvider, ITypeshiftStore<TId>>)`.
3. **`TrySteer` takes a string.** A user who adds an image, or a host that answers an approval while the run goes
   on, has no overload. Proposal: `TrySteer(TId conversation, ChatMessage input)`.
4. **Sub-agents are hidden in plan mode.** A sub-agent's tool carries no `ReadOnly()` mark, so plan mode hides
   every sub-agent, the coding agent's read-only explorer included, which is the one it needs while it plans. A
   sub-agent run in plan mode would also be asked for a plan it has no host to present to. Proposal:
   `UseAgent<T>().ReadOnly()`, which the analyzer checks against the sub-agent's tools (every one `ReadOnly()`, no
   MCP server without `ReadOnly()`), and a sub-agent run in plan mode offers the read-only tools without
   `exit_plan_mode` and without the planning instruction.
5. **A run waits for every background answer.** The coder demo's run was done when the explorer answered, and the
   answer cost one more model call. That is D33's choice and the right default, since an answer must not be lost.
   An `InBackground(wait: false)` for agents whose work is a side effect, with the answer stored but no model call
   made, is worth a thought, not a priority.
6. **Standing approvals cannot be seen or revoked.** `ApproveForConversation()` holds for the rest of the
   conversation, and a UI that shows "always allowed: run_command" or takes it back has no API. Proposal:
   `ConversationTracker.StandingApprovalsAsync(conversation)` and a `RevokeForConversation()` answer, stored like
   the approval.
7. **A conversation loads whole.** Every run loads every entry and scans them three times, although its history
   starts at the last summary. Long conversations pay for all their past on every turn. Proposal, breaking the
   store contract and so before v1: entries carry a `Sequence` the store assigns, `LoadConversationAsync` takes
   the sequence to load from, and the summary entry carries what the scans need from before it: the standing
   approvals and the background calls still open. Stores index by conversation and sequence, which the EF Core
   sample already has.
8. **MCP tools are listed on every run.** `IMcpToolProvider.GetToolsAsync` is called when a run resolves its
   options, a round trip per run unless the provider caches. Proposal: the library caches per provider instance
   and server, and the provider interface gains a way to invalidate, or the guide says to cache and the sample
   does.
9. **The approval loop is written by every host.** `ApprovalRequests`, one answer per request, the plan among
   them, a `ChatMessage` of the answers, the next run. The demos each wrote the same forty lines. Proposal: a
   `ChatResponse.Plan` accessor for the `exit_plan_mode` request, since a plan is answered differently from a
   tool call, and an `Answers` builder that takes a decision per request and yields the message.
10. **Handoffs nest runs.** When a conversation that bounced between agents starts its next run, the host's agent
    runs the answering agent inside its own run, which may run the next inside its own: the support demo's
    follow-up showed `invoke_agent triage_agent` around `billing_agent` around `support_agent`. It works, and the
    spans tell the story, but each hop adds a pipeline and a frame. Proposal: resolve the answering agent in
    `AgentHost` before the pipeline is built, so the run starts at the right agent.
11. **Settings have no schema for a form.** `Settings` serializes to JSON a UI can bind, but a form needs to know
    the members, their types and the tools' overrides. The generator knows all of it. Proposal: a generated
    `Settings.JsonSchema`, so a settings form renders itself.
12. **A hung store hangs a run's end.** Storing never observes the run's token, by design, so a store that never
    answers holds the run after the user cancelled. Proposal: none in the library; the guide should say that a
    store brings its own timeout.

## Before v1

In the order to take them, each small enough for one change:

1. The provider lease, the optional model and the catalog: the multi-provider proposals above. They are the one
   breaking change on the list, and they close the grudge.
2. `TypeshiftBuilder.Services` and the `WithStore` overloads, so a host's plumbing is a library of its own.
3. Entries with a sequence and the store loading from one: the other breaking change, and the one that decides
   whether long conversations stay cheap.
4. `UseAgent<T>().ReadOnly()` and sub-agents in plan mode.
5. `TrySteer(ChatMessage)`, the standing approvals API, the plan accessor and the answers builder.
6. A null for a required argument that is not nullable fails like a missing one.
7. The generated `Settings.JsonSchema`.
8. MCP tool caching.
9. A "Hosting" guide page that shows the provider, the cache, the store and the approval loop in one place, from
   what the demos' shared project holds now.

The audit's fixes are already on the branch. With these nine, the library is the base the demos needed: a host
writes its toolsets and agents, a provider of one method, a store of four, and nothing in between.

## Related

- [Audit](./audit.md): the findings the same pass fixed and left open.
- [Status](./status.md): the known gaps these proposals would close.
- [Decisions](./decisions.md): D1, D25, D28, D33, D34, D42 and D50, which the proposals touch.
- [Running agents](../guide/running-agents.md#chat-clients): the chat client provider as users see it today.
