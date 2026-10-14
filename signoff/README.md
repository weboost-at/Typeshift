# API sign-off

Step 2 of the rewrite: the whole public API, compiled, before any implementation. Nothing here runs; it all builds
warning-free with warnings as errors:

```shell
dotnet build signoff/SignOff.slnx -c Release
dotnet mdsnippets signoff
```

| Path | Holds |
|---|---|
| [`v0-audit.md`](./v0-audit.md) | Step 1: the audit of v0, what carries over and what goes. |
| [`decisions.md`](./decisions.md) | The fresh decisions log, D1 to D49; each entry says whether it is decided, carried from v0, or proposed. |
| [`guide/`](./guide/) | The draft guide, 17 pages; every snippet comes from the compiled samples. |
| [`Typeshift/`](./Typeshift/) | The library's public API as stubs (bodies `throw null!`), the built-in toolsets included. |
| [`Typeshift.Samples/`](./Typeshift.Samples/) | User code for every scenario of the guide. |
| [`Typeshift.Samples/Generated/`](./Typeshift.Samples/Generated/) | Hand-written stand-ins for what the generator would emit for the samples: `Settings`, `{Tool}Overrides`, `Configure{Tool}()`, tool lists, host members, id aliases. |

After the sign-off, `signoff/` goes: the samples move to `samples/`, the guide to `docs/guide/`, the decisions to
`docs/contributing/decisions.md`, and the stubs become the real library slice by slice.

## The API in one screen

```cs
internal partial class TicketToolset : Toolset                 // search_tickets, get_ticket, close_ticket
{
    public TicketToolset()
    {
        AddTool(SearchTickets)
            .WithSettings<SearchTicketsSettings>()
            .Configurable(s => s.Limit);

        AddTool(CloseTicketAsync)
            .RequiresApproval();
    }
}

internal partial class SupportAgent : Agent
{
    public SupportAgent()
    {
        WithChatOptions()
            .WithModel("fast-model")
            .Configurable(o => o.Temperature);

        UseToolset<TicketToolset>().Include(t => t.SearchTickets).Include(t => t.CloseTicket);
        UseToolset<TodoToolset>();
        UseMcpServer("status_page");
        UseContext<CustomerProfileContext>();
        UseHandoff<BillingAgent>();
        WithCompaction().WithMaxMessages(40);

        ConfigureSearchTickets().WithLimit(5);
    }
}

services.AddTypeshift()
    .WithStore<EfTypeshiftStore>()                               // : ITypeshiftStore, ids of TypeshiftIdType
    .WithChatClientProvider<KeyedChatClientProvider>()
    .WithRealtimeTracking()
    .WithToolMiddleware<AuditToolCalls>();

var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();

var settingsId = 42;                                           // whose stored settings: the host decides

await support.ConfigureAsync(settingsId, s => s with { SearchTickets = s.SearchTickets with { Limit = 20 } });
var answer = await support.RunAsync("Where is my refund?", settingsId, conversationId: 7);
var report = await support.RunAsync<RefundStatus>("Summarize the refund.", settingsId);
```

## Decided in this session

- The id type is an MSBuild property, `TypeshiftIdType` (D11).
- Tools get services through `IServiceProvider` only (D15).
- No composition block in v1 (D23).
- After a handoff, the agent handed to answers the conversation's later runs (D34).

## Open for the sign-off

Every entry marked **proposed** in `decisions.md`. The ones with the most weight:

- **D25** runs use the scope the host was resolved from (`AgentHost` transient), replacing v0's scope per tool call.
- **D24** approval through Microsoft.Extensions.AI's contents; no standing approvals or auto-approval rules in v1.
- **D28** the model is always in `Settings` (the brief), though a customer-editable form then picks any model.
- **D38 to D42** the store: four methods, stable kinds, `WithSettingsVersion`, revisions, conversations by their id
  alone (the host authorizes them).
- **D32** compaction by message count with a stored summary, no token triggers yet.
- **D2, D3** diagnostics from an analyzer; discovery stays attribute-free.
- **D13** `Async` is dropped from tool names; renaming a method renames the tool.
- The draft diagnostic catalog, `guide/diagnostics.md` (TSH001 to TSH028).
