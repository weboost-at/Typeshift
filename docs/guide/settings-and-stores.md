---
title: Settings and stores
description: How a run's settings layer from the code through stored settings to the run, and the one store that keeps settings and conversations.
---

# Settings and stores

Typeshift stores each agent's `Settings` under a settings id, and conversations under a conversation id. Both go through
`ITypeshiftStore`, one interface you implement with the id type you already use.

## Settings layering

A run's settings come from four layers, and each sets values over the one below. Tool settings, chat options and run
options all resolve this way, once, when the run starts.

### One value, four layers

Follow the `find_companies` limit of the [getting started](./getting-started.md) agent up through the layers:

```mermaid
flowchart BT
  code["1. The code's defaults: Limit = 5"] --> agent["2. The agent's code: WithLimit(3)"]
  agent --> stored["3. The stored settings: Limit = 2"]
  stored --> run["4. The run: Limit = 1"]
  run --> tool["find_companies runs with Limit = 1"]
```

**1. The code's defaults.** `FindCompaniesSettings` initializes `Limit` to 5, so a record made with its parameterless
constructor holds 5. This layer sets every member, so a tool always receives a whole record.

**2. The agent's code.** `ResearchAgent` calls `ConfigureFindCompanies().WithLimit(3)`, so every run of the agent
searches with 3 unless a layer above sets another limit. Its chat and run options, such as `WithTemperature(0.2f)`,
live here too. Because this layer is reviewed, compiled code, it may set any settable member, configurable or not.

**3. The stored settings.** `ConfigureAsync(42, ...)` stores `Limit = 2` and `Temperature = 0.4f`, so every run with
settings id 42 searches with 2, at a temperature of 0.4. `IncludeInactive` cannot be stored, because
`ResearchAgent.Settings` has only the configurable members.

**4. The run.** A run that passes `FindCompanies = new() { Limit = 1 }` in its settings searches with 1, for that run
only. It leaves `Temperature` null, so the stored 0.4 holds.

### Precedence

The highest layer that sets a value wins:

| Layer | Set by | May set | Holds for |
|---|---|---|---|
| 4. The run | The `settings` argument of `RunAsync` and the other runs | Configurable members | That run |
| 3. Stored | `Settings` under the run's settings id: `ConfigureAsync`, `SaveSettingsAsync` | Configurable members | Every run with that settings id |
| 2. The agent's code | `Configure{Tool}()`, `WithChatOptions()`, `WithRunOptions()`, `GetInstructions()` | Every settable member | Every run of the agent |
| 1. The code's defaults | The settings record's parameterless constructor; the run options' defaults | Every member | Every run |

Because the layers resolve when a run starts, settings saved during a run apply from the next run on.

### Null values and nested records

A layer either replaces a value below it or leaves it alone. It never merges, so a configurable member that is itself
a record or a class is replaced as a whole.

::: warning No layer clears a value
Null keeps the layer below, so neither stored settings nor a run can take away a temperature the agent's code set.
:::

### The model and the instructions

The model resolves through the layers like any other value. It stays as the code sets it unless
`Configurable(o => o.ModelId)` lets the layers above choose it, so stored settings can change the model only when the
agent's code allows that. Its provider is a value of its own, so a layer that sets only `Model` keeps the provider of
the layer below.

Stored or run `Instructions` replace the text that `GetInstructions()` returns. Context providers and plan mode then
append to whatever instructions the layers resolved.

### The mode of a conversation

A run with a conversation id continues in the mode (`Default`, `Plan` or `Auto`) that the conversation's last run
ended in. That mode overrides the agent's code and the stored settings, but not the run's own settings. A run whose
own settings name a mode switches the conversation to it, and so does an approved plan
([The three modes](./tool-approval.md#the-three-modes)).

### Sub-agents and handoffs

Sub-agents and handoffs keep the settings id, but not the run's settings. A sub-agent runs with its own settings stored
under the caller's settings id, in the caller's mode. An agent handed off to answers with its own stored settings under
the same id. Run settings apply only to the agent the run started on ([Multiple agents](./multi-agent.md)).

## Registering a store

Register the store in the `AddTypeshift()` chain:

<!-- snippet: quickstart-registration -->
<a id='snippet-quickstart-registration'></a>
```cs
var services = new ServiceCollection();

services.AddTypeshift()
    .WithStore<InMemoryTypeshiftStore>()
    .WithChatClientProvider<KeyedChatClientProvider>();

services.AddKeyedSingleton<IChatClient>("openai", new CannedChatClient());

await using var provider = services.BuildServiceProvider(validateScopes: true);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L11-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`WithStore<T>()` registers the store as a singleton. `InMemoryTypeshiftStore` is for tests and samples.

Without a store, agents run from their code alone. A run that passes neither a settings id nor a conversation id
works, tools included. Every call that loads or saves, such as a run with either id or `ConfigureAsync`, throws an
`InvalidOperationException` that names `WithStore<T>()`.

## The store

`ITypeshiftStore` has four methods, plus a fifth that a store may override. The ids they take have the build's
[id type](./registration.md#the-id-type), which is `Guid` unless `TypeshiftIdType` names another, and each method takes
a `CancellationToken` last.

| Method | Does |
|---|---|
| `LoadAsync<TData>(id)` | Returns the document of kind `TData.Kind` under an id, with its revision, or null. |
| `SaveAsync<TData>(id, data, revision)` | Saves the document if the stored revision is still `revision` (0 for none) and returns the new revision. Throws a `StoreConflictException` when another save came first. |
| `LoadConversationAsync(conversation)` | Returns a conversation's entries, oldest first. |
| `LoadConversationSinceCompactionAsync(conversation)` | Returns the conversation's last [summary](./conversations.md#compaction) and the entries after it, which is all a run reads. By default it loads the whole conversation and drops what came before; override it to leave those entries in the database. |
| `AppendConversationAsync(conversation, entry)` | Appends one entry: a run's input, a model response, tool results, steering input, the end of a run, or a compaction summary. |

Runs append as they go, so `AppendConversationAsync` is the hot path, and a row per entry keeps it cheap.

## What a store keeps

The documents that `LoadAsync` and `SaveAsync` handle are the agents' `Settings` records. Each serializes itself, with
`Serialize()` and `TData.Deserialize(json, version)`. Next to the JSON, a store keeps three things:

- **The kind**, `TData.Kind`, a stable name. For an agent's settings it is the class name, so moving the agent to
  another namespace keeps its data. `WithStorageKind("support")` in the agent pins it, so renaming the class keeps the
  data too. For two agents with the same kind, the analyzer reports [TSH022](./diagnostics.md#tsh022).
- **The version**, `TData.Version`, which the store hands back to `Deserialize`. `Deserialize` refuses a version newer
  than its code's. Migrating older documents is your store's job, for example in `LoadAsync`, before it hands the
  JSON of an older version to `TData.Deserialize`.
- **The revision**, for optimistic concurrency. A save names the revision it loaded, so it fails when another save came
  first.

A settings id stands for whatever you decide: a customer, a team, a user, or a settings profile of your own. Under one
settings id, each agent has its own document, because each agent's `Settings` is its own kind.

## An Entity Framework Core store

The database keeps documents keyed by kind and id, with the revision as a concurrency token, and conversation entries
in order, each marked whether it is a summary:

<!-- snippet: ef-core-db -->
<a id='snippet-ef-core-db'></a>
```cs
internal sealed class TypeshiftDb(DbContextOptions<TypeshiftDb> options) : DbContext(options)
{
    public DbSet<DocumentRow> Documents => Set<DocumentRow>();

    public DbSet<EntryRow> Entries => Set<EntryRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<DocumentRow>()
            .HasKey(document => new { document.Kind, document.Id });

        model.Entity<DocumentRow>()
            .Property(document => document.Revision)
            .IsConcurrencyToken();

        model.Entity<EntryRow>()
            .HasKey(entry => entry.Sequence);

        model.Entity<EntryRow>()
            .HasIndex(entry => entry.Conversation);
    }
}

internal sealed class DocumentRow
{
    public required string Kind { get; init; }

    public required int Id { get; init; }

    public required int Version { get; set; }

    public required long Revision { get; set; }

    public required string Json { get; set; }
}

internal sealed class EntryRow
{
    public long Sequence { get; init; }

    public required int Conversation { get; init; }

    public required int Version { get; init; }

    public required bool IsSummary { get; init; }

    public required string Json { get; init; }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Stores/EfCore/TypeshiftDb.cs#L5-L54' title='Snippet source file'>snippet source</a> | <a href='#snippet-ef-core-db' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The store maps the five methods onto it, and a run reads a conversation from its last summary only. A save that
another save beat becomes a `StoreConflictException`, and any other database exception reaches the caller unchanged:

<!-- snippet: ef-core-store -->
<a id='snippet-ef-core-store'></a>
```cs
internal sealed class EfTypeshiftStore(IDbContextFactory<TypeshiftDb> contexts) : ITypeshiftStore
{
    public async Task<Stored<TData>?> LoadAsync<TData>(int id, CancellationToken ct)
        where TData : class, IStorable<TData>
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        return await db.Documents.FindAsync([TData.Kind, id], ct) is { } row
            ? new Stored<TData>(TData.Deserialize(row.Json, row.Version), row.Revision)
            : null;
    }

    public async Task<long> SaveAsync<TData>(int id, TData data, long revision, CancellationToken ct)
        where TData : class, IStorable<TData>
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        var row = new DocumentRow
        {
            Kind = TData.Kind,
            Id = id,
            Version = TData.Version,
            Revision = revision + 1,
            Json = data.Serialize()
        };

        var entry = db.Entry(row);
        entry.State = revision == 0 ? EntityState.Added : EntityState.Modified;

        // The update only matches the row while it still has the revision the caller loaded
        if (revision > 0)
            entry.Property(document => document.Revision).OriginalValue = revision;

        // A conflict is another save that won; any other failure is the database's and surfaces as it is
        try
        {
            await db.SaveChangesAsync(ct);
            return row.Revision;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new StoreConflictException($"{TData.Kind} {id} changed after revision {revision}.", exception);
        }
        catch (DbUpdateException exception) when (revision == 0)
        {
            if (!await ExistsAsync(TData.Kind, id, ct)) throw;

            throw new StoreConflictException($"{TData.Kind} {id} was saved first by another.", exception);
        }
    }

    private async Task<bool> ExistsAsync(string kind, int id, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        return await db.Documents.AnyAsync(row => row.Kind == kind && row.Id == id, ct);
    }

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(int conversation, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        var rows = await db.Entries
            .Where(entry => entry.Conversation == conversation)
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(ct);

        return [.. rows.Select(row => ConversationEntry.Deserialize(row.Json, row.Version))];
    }

    // A run starts from the last summary, so the rows before it stay in the database
    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        int conversation,
        CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var entries = db.Entries.Where(entry => entry.Conversation == conversation);

        var start = await entries
            .Where(entry => entry.IsSummary)
            .MaxAsync(entry => (long?)entry.Sequence, ct) ?? 0;

        var rows = await entries
            .Where(entry => entry.Sequence >= start)
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(ct);

        return [.. rows.Select(row => ConversationEntry.Deserialize(row.Json, row.Version))];
    }

    public async Task AppendConversationAsync(int conversation, ConversationEntry entry, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        db.Entries.Add(new EntryRow
        {
            Conversation = conversation,
            Version = ConversationEntry.Version,
            IsSummary = entry.IsSummary,
            Json = entry.Serialize()
        });

        await db.SaveChangesAsync(ct);
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Stores/EfCore/EfTypeshiftStore.cs#L5-L110' title='Snippet source file'>snippet source</a> | <a href='#snippet-ef-core-store' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The store is a singleton, so it cannot take a scoped `DbContext`. It takes an `IDbContextFactory<TypeshiftDb>`
instead and creates a context for each call. Register the factory next to `WithStore<EfTypeshiftStore>()`:

<!-- snippet: ef-core-registration -->
<a id='snippet-ef-core-registration'></a>
```cs
services.AddDbContextFactory<TypeshiftDb>(options => options.UseSqlite("Data Source=typeshift.db"));
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L43-L45' title='Snippet source file'>snippet source</a> | <a href='#snippet-ef-core-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Agent settings

`ConfigureAsync` loads an agent's settings, applies your change and saves them. When another save came in between, it
loads them again and reapplies your change until its save wins, up to ten times; then it throws the store's
`StoreConflictException`:

<!-- snippet: configure-settings -->
<a id='snippet-configure-settings'></a>
```cs
await support.ConfigureAsync(settingsId, settings => settings with
{
    Provider = "anthropic",
    Model = "claude-sonnet-5-5",
    Instructions = "Answer in German.",
    SearchTickets = settings.SearchTickets with { Limit = 20 }
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/AgentSettings.cs#L14-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-configure-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A settings form does it in two steps: it loads the settings with their revision and saves them back with it. A
`StoreConflictException` means someone saved in between:

<!-- snippet: load-and-save-settings -->
<a id='snippet-load-and-save-settings'></a>
```cs
var stored = await support.LoadSettingsAsync(settingsId);

try
{
    await support.SaveSettingsAsync(settingsId, stored.Data with { Instructions = null }, stored.Revision);
}
catch (StoreConflictException)
{
    stored = await support.LoadSettingsAsync(settingsId);
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/AgentSettings.cs#L24-L35' title='Snippet source file'>snippet source</a> | <a href='#snippet-load-and-save-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Saving `Instructions = null` removes the stored instructions, so runs with this settings id use those of
`GetInstructions()` again. After a conflict, the sample loads the settings again, so you can show the user the current
values before they save.

## The stored JSON

What is stored is the record's JSON: camelCase, with the tools nested under `"tools"` by name and nulls left out.

```json
{
  "provider": "anthropic",
  "model": "claude-sonnet-5-5",
  "instructions": "Answer in German.",
  "tools": {
    "search_tickets": {
      "limit": 20
    }
  }
}
```

`System.Text.Json` writes and reads the same JSON, so an endpoint that binds `SupportAgent.Settings` accepts and
returns the JSON the store keeps. A stored member that cannot be read, because its value has the wrong type or its
name is unknown, counts as missing, so the layer below applies. When a configurable member is itself a record or a
class, a stored value replaces the whole object, so members missing from its stored JSON take that type's defaults,
not the values of the layer below.

::: warning A renamed member loses its stored value
A configurable member that is renamed or removed reads as missing, so its stored value no longer applies. When that
matters, raise the agent's settings version with `WithSettingsVersion(2)` and migrate the documents of the older
version in the store.
:::

## Related

- [Tool settings](./tool-settings.md): declaring a tool's settings and opening members to the layers.
- [Generated members](./generated-members.md#settings): the `Settings` record a store keeps.
- [Running agents](./running-agents.md#run-settings): passing a settings id and run settings.
- [Conversations](./conversations.md): what a run stores in a conversation, and resuming it.
- [Registration](./registration.md#the-id-type): the id type and the other chain calls.
