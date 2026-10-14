# Settings and stores

Typeshift persists two things: each agent's `Settings` under a settings id, and conversations under a conversation id.
Both go through one interface you implement once, with your id type.

## The id type

The id type is `Guid` unless `Directory.Build.props` names another:

<!-- snippet: id-type -->
<a id='snippet-id-type'></a>
```csproj
<PropertyGroup>
  <TypeshiftIdType>int</TypeshiftIdType>
</PropertyGroup>
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Typeshift.Samples.Docs.csproj#L5-L9' title='Snippet source file'>snippet source</a> | <a href='#snippet-id-type' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Every project that uses Typeshift sees it: generated members take it (`RunAsync(input, settingsId: 42)`), and
`ITypeshiftStore`, `InMemoryTypeshiftStore`, `ConversationTracker` and `LiveRun` are aliases the generator writes
for it. A referenced project built
with another id type is [TSH021](./diagnostics.md#tsh021); a store of another is [TSH028](./diagnostics.md#tsh028).
The library never creates ids.

## The store

`ITypeshiftStore` has four methods:

- `LoadAsync<TData>(id)` returns the document of kind `TData.Kind` under an id with its revision, or null;
- `SaveAsync<TData>(id, data, revision)` saves it if the stored revision is still `revision` (0 for none), returns the
  new revision, and throws a `StoreConflictException` when another save came first;
- `LoadConversationAsync(conversation)` returns a conversation's entries, oldest first;
- `AppendConversationAsync(conversation, entry)` appends one entry: a run's input, a model response, tool results.
  Runs append as they go, so this is the hot path; a row per entry keeps it cheap.

Documents serialize themselves: `Serialize()` and `TData.Deserialize(json, version)`. Next to the JSON, a store keeps:

- **the kind**, `TData.Kind`: a stable name. For an agent's settings it is the class name, so moving an agent to
  another namespace keeps its data; `WithStorageKind("support")` in the agent pins it, so renaming the class keeps it
  too. Two agents of one kind are [TSH022](./diagnostics.md#tsh022);
- **the version**, `TData.Version`, handed back to `Deserialize`, which refuses a version newer than its code's. A
  store migrates older documents if it needs to;
- **the revision**, for optimistic concurrency.

What a settings id stands for is the host's choice: a customer, a team, a user, or a settings profile of its own.
Under one settings id, each agent has its own document, since each agent's `Settings` is its own kind.

## Registering a store

<!-- snippet: quickstart-registration -->
<a id='snippet-quickstart-registration'></a>
```cs
var services = new ServiceCollection();

services.AddTypeshift()
    .WithStore<InMemoryTypeshiftStore>()
    .WithChatClientProvider<CannedChatClientProvider>();

await using var provider = services.BuildServiceProvider(validateScopes: true);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L10-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`WithStore<T>()` registers the store as a singleton. `InMemoryTypeshiftStore` is for tests and samples.

**Without a store**, agents run from their code: a run without a settings id or a conversation id works, tools run. Every call that loads
or saves throws an `InvalidOperationException` that names `WithStore<T>()`.

## An Entity Framework Core store

<!-- snippet: ef-core-db -->
<a id='snippet-ef-core-db'></a>
```cs
internal sealed class TypeshiftDb(DbContextOptions<TypeshiftDb> options) : DbContext(options)
{
    public DbSet<DocumentRow> Documents => Set<DocumentRow>();

    public DbSet<EntryRow> Entries => Set<EntryRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<DocumentRow>().HasKey(document => new { document.Kind, document.Id });
        model.Entity<DocumentRow>().Property(document => document.Revision).IsConcurrencyToken();
        model.Entity<EntryRow>().HasKey(entry => entry.Sequence);
        model.Entity<EntryRow>().HasIndex(entry => entry.Conversation);
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

    public required string Json { get; init; }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Stores/EfCore/TypeshiftDb.cs#L5-L44' title='Snippet source file'>snippet source</a> | <a href='#snippet-ef-core-db' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

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

        try
        {
            await db.SaveChangesAsync(ct);
            return row.Revision;
        }
        catch (DbUpdateException exception)
        {
            throw new StoreConflictException($"{TData.Kind} {id} changed after revision {revision}.", exception);
        }
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

    public async Task AppendConversationAsync(int conversation, ConversationEntry entry, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        db.Entries.Add(new EntryRow
        {
            Conversation = conversation,
            Version = ConversationEntry.Version,
            Json = entry.Serialize()
        });

        await db.SaveChangesAsync(ct);
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Stores/EfCore/EfTypeshiftStore.cs#L5-L76' title='Snippet source file'>snippet source</a> | <a href='#snippet-ef-core-store' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: ef-core-registration -->
<a id='snippet-ef-core-registration'></a>
```cs
services.AddDbContextFactory<TypeshiftDb>(options => options.UseSqlite("Data Source=typeshift.db"));
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L37-L39' title='Snippet source file'>snippet source</a> | <a href='#snippet-ef-core-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Agent settings

`ConfigureAsync` loads an agent's settings, applies your change and saves them; when another save came in between,
it loads them again and applies the change again, until its save wins:

<!-- snippet: configure-settings -->
<a id='snippet-configure-settings'></a>
```cs
await support.ConfigureAsync(settingsId, settings => settings with
{
    Model = "reasoning-model",
    Instructions = "Answer in German.",
    SearchTickets = settings.SearchTickets with { Limit = 20 }
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/AgentSettings.cs#L14-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-configure-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A settings form loads them with their revision and saves them back with it; a conflict means someone saved in
between:

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/AgentSettings.cs#L23-L34' title='Snippet source file'>snippet source</a> | <a href='#snippet-load-and-save-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

What is stored is the record's JSON: camelCase, tools nested under `"tools"` by name, nulls left out.

```json
{
  "model": "reasoning-model",
  "instructions": "Answer in German.",
  "tools": {
    "search_tickets": {
      "limit": 20
    }
  }
}
```

`System.Text.Json` writes and reads the same JSON, so an endpoint that binds `SupportAgent.Settings` speaks the stored
shape. A member that does not read, mistyped or unknown, keeps the layer below; a stored record or class is replaced
as a whole, its missing members taking their defaults.

A configurable member renamed or removed reads as missing. When that matters, raise the agent's settings version with
`WithSettingsVersion(2)` and migrate the documents of the older version in the store.
