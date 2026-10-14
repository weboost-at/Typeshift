# v0: what carries over, what goes

Step 1 of the rewrite: v0 (`main` at `edb5105`) read through `AGENTS.md`, the decisions, `status.md` and the guide, then
audited with `ponytail-audit` (runtime and generator separately). Load assumed: weboost's multi-tenant SaaS, many
concurrent runs over several processes, settings and conversations per customer.

## Audit findings that shape the rewrite

Runtime:

1. **Any caller can resume any conversation.** A stored conversation has no owner; a leaked or guessed id resumes and
   appends to another customer's history. Conversations are keyed by owner and id from day one.
2. **Tools and the chat client provider never learn the customer.** Each tool call gets a fresh scope from the root
   container, so a request-scoped tenant service is empty inside tools. Runs resolve everything from the scope the host
   runs in.
3. **A failed conversation save throws away a finished answer** (and tools with side effects run twice on retry).
   Save failures are logged and traced; the answer returns.
4. **Every model call awaits event handlers** (up to 5 s per session event, 250 ms per publish). Live updates go through
   a buffered feed the subscriber reads at its own pace; a run never waits for a subscriber.
5. **Each turn reads the conversation twice and rewrites it whole** (quadratic in its length). The store appends one
   session per turn.
6. **The agent's name rides to the provider** in `ChatOptions.AdditionalProperties`. It goes on `ChatMessage.AuthorName`
   and the tracker's own state instead.
7. **Disposing an agent's client disposes the provider's shared client.** The provider's client is wrapped so it is
   never disposed by Typeshift.

Generator:

1. `ref`/`out` tool parameters, tools declared outside the constructor body, a member named `Tools`, nested-name
   clashes (`Outer.Agent` vs `OuterAgent`), private nested classes, missing parameterless constructors, keyword names
   and `get_v2`/`get_v_2` all end in CS errors or silent misses. Each gets a TSH diagnostic.
2. Diagnostics are reported without syntax trees, so `#pragma` and `.editorconfig` cannot reach them, and locations in
   the models break caching. A `DiagnosticAnalyzer` reports them; the generator only writes code.
3. `var u = UseToolset<T>(); u.Include(...)` diverges silently from runtime (D29). Every form the generator cannot read
   is diagnosed.
4. Stamps carry only names, so a toolset package built with one generator version and used with another can break.
   Stamps carry the generated type names and a stamp version.
5. Discovery re-binds every class with a base list on each edit (about 170 ms per keystroke for 50 toolsets). The
   syntax predicate narrows to classes whose base list names `Toolset`, `Agent` or `AgentPreset`.
6. The Roslyn 4.14 pin protects nothing (consumers need C# 14 anyway); the generator targets Roslyn 5.

## Carries over

- **Declarations:** partial classes declaring everything in the constructor; one flat chain per declaration; the
  allow and deny lists of configurable members whose lambdas never run (D5, D43); settings as a tool parameter the
  model never sees (D40); `GetInstructions()` (D42); `Include`/`Exclude` from a generated tool list (D29);
  `Configure{Tool}()` (D30); the generated `Settings` and `{Tool}Overrides` records with null meaning "the layer
  below" (D27).
- **Registration:** the intercepted `AddTypeshift()` with a throwing fallback (D1), modules registering once per
  collection, JSON contexts registered by the modules (D44), the hidden public surface generated code calls (D35).
- **Tool invocation:** generated `AIFunction`s binding through `JsonTypeInfo`, tolerant of arguments sent as JSON
  text; schemas from STJ contracts, cached, nullable roots widened, compared against `AIFunctionFactory` (D12).
- **Storage:** one store of self-serializing documents (`IStorable` with a static abstract `Deserialize`, D25); the
  store as the persistence switch, throwing with what to register (D26); member-by-member JSON without reflection.
- **Conversations:** tracking as a `DelegatingChatClient` outside function invocation; the provider never receives
  the conversation id (D18); unanswered calls dropped on resume; cancelled calls stored; a time limit per subscriber.
- **Generator:** parse per class, toolset summaries, agents resolved in their own step; models of strings, bools and
  `EquatableArray`; named steps with the caching test; constructor chains through `IOperation`; `DefaultValues`,
  `XmlDocs`, `SourceWriter`; stamps as the only cross-assembly channel.
- **Repository:** the playbooks, the decisions log, guard tests (test conventions, public API docs, decision
  citations, source conventions, diagnostic catalog, caching), the scripted `IChatClient`, `FakeTimeProvider` with
  `TaskCompletionSource` gates, docs samples run in CI, the package consumer, release by tag.

## Goes

- **Stored tool defaults** (`ToolDefaults`, one load per tool) and the fifth settings layer.
- **The three-step run** as the common path (it stays public as the raw path).
- **`ToolCatalog`** (D10): nothing calls it since agents as data went.
- **`ChatExecutionContext`** and its extensions; **process-wide awaited events**; the per-process store locks and the
  second conversation read.
- **Three tool binding kinds** and their factories: one binding.
- **Required `WithName` on tools** (TSH003): names derive from the method.
- **`ITypeshiftStore<TId>` keyed by `typeof(TData).FullName`:** kinds become stable names.
- **The diagnostic plumbing in models** (`DiagnosticBag`, `DiagnosticInfo`, `LocationInfo`).
- **Per-call DI scopes** (D11): calls use the run's scope.
- **Silently unstored conversation ids** that do not parse (D33): the run API takes typed ids; the raw path throws.
- **Error content replayed to the model** on resume.

## Behaviors to cover again

`status.md`'s table stays the checklist. Rows 25 and 26 (stored tool defaults) go with the fifth layer; 22, 23 and
35 to 40 stay as v0 left them; row 44's "id that does not parse" becomes an exception.
