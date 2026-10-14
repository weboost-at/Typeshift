# Typeshift, rebuilt: the brief

You are rebuilding Typeshift from scratch in this repository. The current code (v0, `main` at `edb5105`) is prior
art: read it for the lessons, the conventions and the edge cases it found, not as code to keep. Everything below is
what the owner cares about. The API sketches show direction, not the final shape: the shape is settled with the owner
before any implementation (see [Process](#process)).

Work on the branch `claude/rewrite`, created from the branch that holds this brief. Replace `src/`, `tests/`,
`samples/` and the docs content; keep the git history, the CI and release workflows, the tooling setup (including
`.claude/hooks/session-start.sh`) and the repository conventions, rewritten where the new design needs it. Ask the
owner whenever a choice is theirs to make; be concise with them. Work with the [skills](#skills) below from the
first step.

## What Typeshift is

A .NET 10 / C# 14 library for code-defined agents and toolsets on Microsoft.Extensions.AI, plus the Roslyn
incremental generator that powers it. Plain fluent C# declares tools, agents and their settings; the generator turns
it into reflection-free `AIFunction`s, a typed settings contract per agent, typed runtime members and one
registration call. Misuse fails the build.

Audience: an open-source library on nuget.org, with weboost's multi-tenant SaaS as the reference host: settings
stored per customer, a settings UI bound to the generated contract, conversations stored per customer.

Build on Microsoft.Extensions.AI, not on Microsoft Agent Framework. Do learn from MAF and take what improves this
library, starting with its harness capabilities:
https://learn.microsoft.com/en-us/agent-framework/concepts/harness?pivots=programming-language-csharp#harness-capability-matrix
(read it if your network reaches it; otherwise work from what you know of MAF).

## What the owner cares about

- **Mistakes fail the build.** Including a tool of another toolset, configuring a tool the agent does not offer,
  marking a member configurable that cannot be stored: compiler errors or TSH diagnostics, never runtime surprises.
  Every shape the generator cannot handle gets a TSH diagnostic, never a CS error inside generated code.
- **Typed throughout.** No `object` catch-alls, no string-keyed dictionaries, no `params Delegate[]`, no reflection
  over user types on generated paths. JSON through `JsonTypeInfo`.
- **One flat fluent chain per declaration, everywhere.** No builder inside a lambda: not in toolsets, not in agents,
  not in registration. Metadata goes through `With...` calls in every kind of declaration.
- **One way to do one thing.** No second API next to the first "for flexibility".
- **Settings the model never sees.** A tool receives its settings as a parameter that is not in the schema.
  Configurable members are an allow list by default.
- **A settings contract per agent.** A generated `Settings` record with exactly what may change at runtime, the model
  included, usable as a UI or API contract.
- **The host owns ids and storage.** The library never creates ids. A missing store makes persistent APIs throw,
  naming what to register; it never silently stores nothing.
- **Simplicity over cleverness.** Fewer layers, fewer types, fewer concepts. When a design needs a long explanation,
  look for a simpler one.
- **Native AOT is supported, not driving.** It must work and CI proves it, but it does not get to dictate an awkward
  API or narrow what users may write.
- **The code reads as if the owner wrote it.** Terse, intention-revealing, comment-free by default. Docs in short
  plain sentences.

## Scope of v1

**Core** (as in v0, simplified):

- Toolsets of static-method tools; tool settings with configurable members; agents with chat options, instructions,
  tool selection and tool configuration; `AgentHost<TAgent>` to run them.
- Registration through the intercepted `services.AddTypeshift()` chain, covering the project and its references.
- One store, ids typed end to end, `Guid` by default and configurable.
- Conversation tracking: every exchange stored as a session of its conversation, streaming included, resumed by id.
  Live, realtime publishing of streaming updates is opt-in; its intervals and limits are options.

**Taken from MAF** (all in v1):

- Tool approval (human in the loop): a tool marked as needing approval pauses the run until the host approves or
  rejects; reuse Microsoft.Extensions.AI's approval primitives where they exist.
- Middleware around agent runs and around each tool call.
- Context compaction: history reduction or summarization as a conversation grows.
- Structured output: `RunAsync<T>()` returning a typed result.
- An agent offered as a tool to another agent; handoff between agents.
- MCP server tools inside an agent's tools.
- Context providers: memory, retrieval and other context injected per run.
- OpenTelemetry traces and metrics for runs, tool calls and tokens.

**Built-in harness toolsets**, in the core package, written with Typeshift's own toolset API (dogfooding):

- todo / planning;
- file system, within a root the host chooses;
- shell / code execution, behind approval;
- web fetch and web search, behind interfaces the host implements (no provider ships; Firecrawl, Tavily and the
  like plug in).

**Stays out:** agents as data (stored or runtime-defined agents), unique agent names, provider SDKs (samples use a
scripted client), a dependency on MAF.

## API direction

Sketches, to be refined in the API sign-off. Names marked as decided are the owner's.

### A toolset

```cs
internal partial class TicketToolset : Toolset
{
    public TicketToolset()
    {
        WithDisplayName("Tickets");

        AddTool(SearchTickets)
            .WithSettings<SearchTicketsSettings>()
            .Configurable(s => s.IncludeClosed)
            .Configurable(s => s.Limit);

        AddTool(CloseTicketAsync)
            .RequiresApproval();
    }

    /// <summary>
    /// Searches the customer's support tickets, newest first.
    /// </summary>
    /// <param name="query">Words the ticket's title contains.</param>
    public static IReadOnlyList<Ticket> SearchTickets(
        string query,
        SearchTicketsSettings settings,
        IServiceProvider services) =>
        services.GetRequiredService<ITicketClient>().Search(query, settings.IncludeClosed, settings.Limit);
}
```

- Tool names derive from the method: `SearchTickets` is `search_tickets`. `WithName(...)` stays as an optional
  override. Decide how an `Async` suffix and a rename (which changes the stored key) are handled.
- Descriptions come from XML docs, `WithDescription(...)` overrides.
- `WithSettings<T>().Configurable(...)` (allow list) and `WithConfigurableSettings<T>().NotConfigurable(...)` (deny
  list) stay. The picks stay lambdas the generator reads and that never run.
- Tools stay static, receiving services through an `IServiceProvider` parameter, the settings record, the token and
  the raw arguments. Investigate injecting resolved services directly as method parameters
  (`ITicketClient tickets`) and bring the options to the sign-off: how the generator tells a service from a model
  argument, what it costs in diagnostics and docs.
- The per-tool record of configurable members stays `{Tool}Overrides`.

### An agent

```cs
internal partial class SupportAgent : Agent
{
    public SupportAgent()
    {
        WithName("support_agent");
        WithDescription("Answers customers from their tickets and the help center.");

        WithChatOptions()
            .WithModel("fast-model")
            .WithTemperature(0.3f)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature);

        UseToolset<TicketToolset>().Include(t => t.SearchTickets);

        UseToolset<KnowledgeToolset>().Exclude(t => t.DeleteArticle);

        UseToolset<TodoToolset>();

        ConfigureSearchTickets()
            .WithLimit(5);
    }

    protected override string GetInstructions() =>
        """
        Answer from the customer's tickets and cite help center articles.
        """;
}
```

- Decided renames: `AgentDefinition` is `Agent`, `AgentRuntime<TAgent>` is `AgentHost<TAgent>`.
- Keep: `Include`/`Exclude` from a generated, typed tool list; a generated typed configuration entry per offered tool
  (`Configure{Tool}()` today; refine if a better shape fits the one-chain rule); `GetInstructions()` as a virtual
  method; the generated `Settings` record; the model as a setting.
- No agent inheritance. Shared configuration (a common toolset selection, common options) comes from composition;
  design the building block in the sign-off.

### Registration

```cs
services.AddTypeshift()
    .WithIdType<int>()
    .WithStore<EfTypeshiftStore>()
    .WithChatClientProvider<KeyedChatClientProvider>()
    .WithRealtimeTracking();
```

- One flat chain; the generator intercepts `AddTypeshift()` and reads the chain. The method names are a sketch.
- The id type defaults to `Guid` and is configured once; generation carries it into every typed member, the store
  and the conversation ids.
- `IChatClientProvider` stays: the host maps a model name to an `IChatClient`.

### Running

```cs
var support = provider.GetRequiredService<AgentHost<SupportAgent>>();

await support.ConfigureAsync(customer, settings => settings with
{
    Model = "reasoning-model",
    SearchTickets = settings.SearchTickets with { Limit = 20 }
});

var answer = await support.RunAsync("Where is my refund?", id: customer, conversationId: conversation);

var report = await support.RunAsync<RefundReport>("Summarize the refund.", id: customer);

await foreach (var update in support.RunStreamingAsync("Where is my refund?", id: customer))
    Console.Write(update.Text);
```

- `RunAsync`, `RunAsync<T>` and `RunStreamingAsync` for the common case; the three-step path (resolve options,
  create the client, call it) stays public for hosts that need the raw `IChatClient`.
- The conversation id is typed in the run API; parsing a string remains only on the raw `IChatClient` path.
- Run settings are the agent's `Settings`, overriding for one run.

### Settings and the store

Four layers, lowest first: the settings record's code defaults, the agent's code, the agent's stored `Settings`, the
run. v0's stored tool defaults (`ToolDefaults`, one store load per tool) are gone.

```cs
internal sealed class EfTypeshiftStore(IDbContextFactory<AppDb> contexts) : ITypeshiftStore
{
    public Task<TData?> LoadAsync<TData>(int id, CancellationToken ct) where TData : class, IStorable<TData> => ...

    public Task SaveAsync<TData>(int id, TData data, CancellationToken ct) where TData : class, IStorable<TData> => ...
}
```

- Decided rename: `ITypeshiftStore<TId>` is `ITypeshiftStore`, its id typed by the registration. How a library
  interface gets the host's id type is for the sign-off (generated into the host, a typed wrapper, or another way).
- Fix v0's store gaps from day one: stable kind names instead of `typeof(TData).FullName`, a schema version per
  document, optimistic concurrency, and loading everything a run needs in one round trip.
- Documents stay a closed set of known types, so store generators for Marten or EF Core stay possible later.

## What to do differently

- **Settle the API before building it.** v0 reworked tool selection three times (delegates, generated methods,
  `Include`/`Exclude`), tool settings three times (`WithToolSettings(object)`, overloads, `Configure{Tool}()`), the
  configurable mode twice, and went from four store interfaces to one, all after the alpha was built.
- **Report diagnostics where users can silence them.** v0's generator reported TSH diagnostics without syntax trees,
  so `#pragma` and `.editorconfig` could not reach them. Consider a `DiagnosticAnalyzer` beside the generator, which
  also keeps the generator's pipeline clean.
- **Prefer API the generator reads semantically.** v0's tool picks did not bind inside the generator's own
  compilation, so it read them from syntax and a split chain silently diverged from runtime. Avoid such shapes, or
  diagnose every form the generator cannot read.
- **Cover the generator's edge cases with diagnostics from the start.** v0 failed with CS errors on private nested
  classes, classes without a parameterless constructor, keyword-named classes, a toolset named like a generated
  member, and tool names that collide after case conversion.
- **Let AOT stop driving.** Widen what may be configurable beyond v0's TSH014 list (nested records, lists,
  dictionaries) where the generator can serialize it; give tool parameter and result types a path that does not
  require a hand-written `JsonSerializerContext` outside AOT, and an analyzer that reports missing types under AOT.
- **Make fixed numbers options.** v0 hard-coded the tracking intervals and subscriber limits.
- **Fewer concepts overall.** Four settings layers instead of five, one run call instead of three steps, one metadata
  style, one registration chain.

## Skills

The session-start hook installs these into `~/.claude/skills`, pinned to commits the owner reviewed, together with
the .NET SDK, the repository's tools and packages. If a skill is missing, run `.claude/hooks/session-start.sh` with
`CLAUDE_CODE_REMOTE=true`, or read its `SKILL.md` there directly.

| Skill | When |
|---|---|
| `ponytail`, level full | Load it first; it stays on for the whole session. The owner's scope is not cut; within each feature, take the smallest API and the least code that fully work. |
| `ponytail-audit` | On v0 in step 1 of the process, as input to what carries over; on the rewrite before the PR. |
| `ponytail-review` | After every slice and before the PR. Fix every "Must fix" before the next slice. |
| `ponytail-debt` | At the end of every slice: carry each `shortcut:` comment into `status.md`'s known gaps. |
| `csharp-test-formatting` | Every test you write or change; its convention tests become the repository's guard tests. |
| `test-data-catalog` | All test data: `TestValue<T>`, the seeded `Data` catalog, builders. |
| `git-commit` | Before every commit: the message style, staging by pathspec, the lint script on the draft. |
| `git-worktree` | Only for parallel work in worktrees (subagents, say). Work lands on `claude/rewrite`, never on `main`. |

Where a skill and this brief or the repository's rules disagree, the brief and the repository win, except:

- `shortcut: <the limit>, <when to upgrade>` comments are allowed, the one marker besides `// Dnn:`. TODOs stay
  banned; `ponytail-debt` keeps the shortcuts and `status.md` in step.
- `ponytail`'s "one small test" is a floor. The repository's test rules and the behavior coverage still apply.

## Process

1. **Read v0.** `AGENTS.md`, `docs/contributing/decisions.md`, `status.md` (its behavior table is a checklist of
   behaviors to cover again where they still hold) and the guide, then run `ponytail-audit` on it. List what carries
   over and what goes.
2. **API sign-off.** Before any implementation: compiled usage samples for every scenario of the guide, draft guide
   pages, and decision entries for each open question above. Present them to the owner and wait for approval.
3. **Build in vertical slices.** Each slice brings code, tests, docs and decision entries together, in commits that
   each build and pass. End each slice with `ponytail-review` and `ponytail-debt`.
4. **Review pass:** `ponytail-review` on the whole branch and `ponytail-audit` on the rewrite, fix what they find,
   then update `status.md` and open the PR.

## Repository conventions

Keep v0's conventions, rewritten to fit the new code:

- `AGENTS.md` as the task map with traps, plus playbooks in `.claude/skills/`.
- A fresh decisions log (D1, D2, ...) with context, decision, alternatives and where it lives; code cites entries as
  `// Dnn: ...`; carry v0 decisions that still hold into it.
- Guard tests for the repository's rules: test conventions, public API docs, decision citations, source style, the
  diagnostic catalog, incremental caching.
- Strict tests, as in v0 and as `csharp-test-formatting` and `test-data-catalog` teach them: xUnit v3,
  FluentAssertions 7 pinned, time only through `FakeTimeProvider`.
- A VitePress docs site whose every snippet comes from compiled samples that CI runs (mdsnippets).
- v0's code style (`AGENTS.md`), commits as `git-commit` writes them, the TSH diagnostic prefix, the Typeshift name,
  releases by tag through nuget.org trusted publishing.

## Done means

- The build is warning-free with warnings as errors; the whole suite is green three times in a row.
- The console sample publishes as Native AOT with zero trim and AOT warnings and runs; the package consumer builds
  and runs against the packed package.
- The docs samples run, the snippets match, the docs site builds without dead links, slopwatch is clean.
- `status.md` lists what is open, the decisions log explains what is not obvious.
