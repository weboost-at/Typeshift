---
title: Implementing a store
description: Write the store that keeps your agents' settings and conversations in your own database, from its lifetime and revisions to conversation summaries.
---

# Implementing a store

Typeshift keeps your agents' settings through an `ISettingsStore` and your conversations through an
`IConversationStore`, which you implement over your own database. This page walks through what a store must do, in the
order you meet it: its lifetime, the settings it saves with their revisions, and the conversations it appends to.
`InMemoryTypeshiftStore` is for tests and samples, so a production app writes its own.

## The store is a singleton

`WithStore<T>()` registers your store as a singleton. One instance handles every run of every request, and several
runs often call it at the same time, so plan for that before you write its first method:

- **It cannot take scoped services**, such as a `DbContext` or the current customer. Take a factory instead, such as
  `IDbContextFactory<T>` or `IServiceScopeFactory`, and create what a call needs inside the call. Singleton services,
  such as options or a logger, are fine.
- **Any method can run while others are running**, even on the same document or conversation. Keep no state between
  calls, or guard what you keep.
- **It knows nothing of the caller.** Your app decides who may use a settings id or a conversation id before it
  passes one to a run ([Conversations](./conversations.md#tracking-a-conversation)), and the store keeps whatever it
  is given.

::: warning A scoped DbContext breaks the store
A singleton that takes a `DbContext` keeps the first one it got for the life of the app and shares it between
requests, which `DbContext` does not allow. Register `IDbContextFactory<T>` with `AddDbContextFactory` and create a
context in each method.
:::

This page builds a store on Entity Framework Core. Register its context factory next to your `WithStore<T>()` call:

<!-- snippet: ef-core-registration -->
<a id='snippet-ef-core-registration'></a>
```cs
services.AddDbContextFactory<TypeshiftDb>(options => options.UseSqlite("Data Source=typeshift.db"));
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L43-L45' title='Snippet source file'>snippet source</a> | <a href='#snippet-ef-core-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## The five methods

A store has five methods in two interfaces. `ISettingsStore` keeps settings with the first two, and `IConversationStore`
keeps conversations with the other three. The store on this page implements `ITypeshiftStore`, which is both. When
settings and conversations live in different databases, implement each interface in a class of its own and
[register each](./settings-and-stores.md#registering-a-store).

You implement four of the methods, and `LoadConversationSinceCompactionAsync` has a default you may replace by declaring
the method in your store. Every id has your build's [id type](./registration.md#the-id-type), which is `Guid` unless
`TypeshiftIdType` names another; the samples on this page set it to `int`. Each method takes a `CancellationToken` last.

| Method | Does |
|---|---|
| `LoadAsync(id, kind)` | Returns the document of a kind under an id as a `StoredDocument`, with its JSON and revision, or null when none is stored. |
| `SaveAsync(id, kind, json, revision)` | Saves a document's JSON if its stored revision still equals `revision` (0 when nothing is stored yet), and returns the new revision. Throws a `StoreConflictException` when another save came first. |
| `LoadConversationAsync(conversation)` | Returns a conversation's entries, oldest first. |
| `LoadConversationSinceCompactionAsync(conversation)` | Returns the conversation's last summary and the entries after it ([Recognizing a summary](#recognizing-a-summary)). |
| `AppendConversationAsync(conversation, entry)` | Appends one entry to a conversation. The first entry creates the conversation. |

The database on this page has a table for settings and one for conversations:

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
            .HasKey(document => new { document.Id, document.Kind });

        model.Entity<EntryRow>()
            .HasKey(entry => entry.Sequence);

        model.Entity<EntryRow>()
            .HasIndex(entry => entry.Conversation);
    }
}

internal sealed class DocumentRow
{
    public required int Id { get; init; }

    public required string Kind { get; init; }

    public required long Revision { get; init; }

    public required string Json { get; init; }
}

internal sealed class EntryRow
{
    public long Sequence { get; init; }

    public required int Conversation { get; init; }

    public required bool IsSummary { get; init; }

    public required string Json { get; init; }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Stores/EfCore/TypeshiftDb.cs#L5-L46' title='Snippet source file'>snippet source</a> | <a href='#snippet-ef-core-db' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Settings documents

A document is an agent's `Settings` record, the one `ConfigureAsync` and `SaveSettingsAsync` save. Typeshift writes
the record as JSON before it calls `SaveAsync` and reads it back after `LoadAsync`, so your store handles strings and
numbers only. Keep each document in a row keyed by the id and the kind, with the JSON and the
[revision](#revisions):

- **The id** is the settings id your app passed, such as a customer's.
- **The kind** is the agent's class name, or what its `WithStorageKind("...")` sets. One settings id holds a document
  per agent of a run, sub-agents and handoffs included
  ([Sub-agents and handoffs](./settings-and-stores.md#sub-agents-and-handoffs)), so the id and the kind together are
  the key.
- **The JSON** is the record as Typeshift wrote it ([The stored JSON](./settings-and-stores.md#the-stored-json)).
  Keep it as text and return it unchanged.

The JSON names the agent's schema version, the one `WithSettingsVersion(n)` sets, in `"$version"`
([The stored JSON](./settings-and-stores.md#the-stored-json)), and JSON without it is at version 1. Typeshift refuses
a version newer than the code's, so an app rolled back to older code fails loudly instead of reading settings it does
not understand.

An agent raises its version when a configurable member is renamed or removed, and migrating the stored JSON of older
versions is then up to you. You can do it in `LoadAsync`, where the kind tells you which agent's settings a row
holds: read `"$version"`, rewrite the JSON of an older version and return it with the revision the row holds, or
migrate the rows with a script over your database. Each save writes the current version, so a migrated document
reaches the new version in your database with its next save.

Loading reads the row by its id and kind and returns its JSON and revision as a `StoredDocument`:

<!-- snippet: ef-core-load -->
<a id='snippet-ef-core-load'></a>
```cs
internal sealed class EfTypeshiftStore(IDbContextFactory<TypeshiftDb> contexts) : ITypeshiftStore
{
    public async Task<StoredDocument?> LoadAsync(int id, string kind, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        return await db.Documents.FindAsync([id, kind], ct) is { } row
            ? new StoredDocument(row.Json, row.Revision)
            : null;
    }
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Stores/EfCore/EfTypeshiftStore.cs#L5-L16' title='Snippet source file'>snippet source</a> | <a href='#snippet-ef-core-load' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Revisions

A revision is the number of the stored document's current state, kept for optimistic concurrency. It lets a save
find out that the document changed since the caller loaded it, so one save never silently overwrites another.
Typeshift does not interpret the number: it hands `SaveAsync` back the revision `LoadAsync` returned, and your store
compares the two.

```mermaid
sequenceDiagram
  participant A as Settings form A
  participant S as Your store
  participant B as Settings form B
  A->>S: LoadAsync: revision 3
  B->>S: LoadAsync: revision 3
  A->>S: SaveAsync with revision 3
  S-->>A: Saved, revision 4
  B->>S: SaveAsync with revision 3
  S-->>B: StoreConflictException
```

The diagram shows two forms editing the same customer's settings. Both load revision 3, and the first to save
wins and moves the document to revision 4. The second still names revision 3, so its save fails instead of erasing
the first form's change, and that form loads the settings again.

### What a save must do

Run one `INSERT` or `UPDATE` that checks the revision as it writes, and throw a `StoreConflictException` when it
changed no row. The revision the caller passes tells you which state of the document it loaded:

- **Revision 0 means the caller found no document**, so insert it. A row that is there already was created by another
  save in between.
- **Any other revision is the one the caller loaded**, so update the row only while it still holds that revision. A
  row that holds another revision was saved by someone else since, and a missing row was deleted since. Both are
  conflicts.

Check and write in one statement. If you read the row, compare its revision and then write, two saves that both
loaded revision 3 can both pass the check before either writes, and the second silently overwrites the first. Each of
these statements reports how many rows it changed, and none means another save won:

```sql
INSERT INTO documents (id, kind, revision, json) VALUES (@id, @kind, 1, @json)
ON CONFLICT (id, kind) DO NOTHING;

UPDATE documents SET json = @json, revision = @revision + 1
WHERE id = @id AND kind = @kind AND revision = @revision;
```

SQL Server has no `ON CONFLICT`. There, run a plain `INSERT` and treat the duplicate-key error, number 2627, as the
conflict, passing it to the `StoreConflictException` as its inner exception.

Return the new revision. Any number works as long as it is not 0 and was never this document's revision before, and
counting up by one is the simple choice. Throw `StoreConflictException` only for a lost race, with a message that
names the document. Any other failure, such as a lost connection, goes to the caller as it is.

The EF Core store runs one of the two statements per save. `ExecuteSqlAsync` inserts with `ON CONFLICT DO NOTHING`,
which SQLite and PostgreSQL support, and turns each interpolated value into a SQL parameter. `ExecuteUpdateAsync`
updates with the revision in its `WHERE`. Both return the number of rows they changed:

<!-- snippet: ef-core-save -->
<a id='snippet-ef-core-save'></a>
```cs
public async Task<long> SaveAsync(int id, string kind, string json, long revision, CancellationToken ct)
{
    await using var db = await contexts.CreateDbContextAsync(ct);

    var rows = revision == 0
        ? await InsertAsync()
        : await UpdateAsync();

    // Each statement checks and writes in one step, so a save that changed no row lost to another save
    return rows == 1
        ? revision + 1
        : throw new StoreConflictException($"{kind} {id} was saved after revision {revision}.");

    Task<int> InsertAsync() =>
        db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "Documents" ("Id", "Kind", "Revision", "Json")
            VALUES ({id}, {kind}, 1, {json})
            ON CONFLICT ("Id", "Kind") DO NOTHING
            """,
            ct);

    Task<int> UpdateAsync() =>
        db.Documents
            .Where(row => row.Id == id && row.Kind == kind && row.Revision == revision)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(row => row.Json, json)
                    .SetProperty(row => row.Revision, revision + 1),
                ct);
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Stores/EfCore/EfTypeshiftStore.cs#L18-L50' title='Snippet source file'>snippet source</a> | <a href='#snippet-ef-core-save' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Who saves

Runs only load settings; they never save them. Saves come from your app, through `ConfigureAsync`, which loads again
and reapplies its change after a lost save, and `SaveSettingsAsync`, which leaves the conflict to your code
([Agent settings](./settings-and-stores.md#agent-settings)).

## Conversations

A conversation only grows. Runs append each step as it happens: the input, every model response, the tool results
of each round of calls, steering input and the entry that ends a run
([What is stored](./conversations.md#what-is-stored)). Your store never changes or removes an entry:

- **Keep the append order.** `LoadConversationAsync` returns the entries in the order they were appended, oldest
  first. Let the database number the rows, as an identity column does; a timestamp can tie.
- **Store each entry as its JSON**, `entry.Serialize()`, and read it back with `ConversationEntry.Deserialize(json)`.
  A database client that serializes with `System.Text.Json` or Newtonsoft.Json may take the entry as it is
  ([What is stored](./conversations.md#what-is-stored)).
- **Keep the append cheap.** Runs append while they work, so `AppendConversationAsync` is the hot path, and a row per
  entry suits it.

### Recognizing a summary

When clearing old tool results does not shrink the history enough, [compaction](./conversations.md#compaction)
summarizes a run's oldest turns. It appends one entry of its own that holds the summary and the messages it kept after
the summary, with `IsSummary` set to true. Every later run starts from the latest such entry, because it carries what
the runs need from before it, including the conversation's mode and its standing approvals.

`LoadConversationSinceCompactionAsync` returns that entry and everything after it, oldest first, or every entry when
the conversation has no summary. Its default calls `LoadConversationAsync` and drops what came before the summary, so
a long conversation is read whole on every run. To read only what a run needs, copy `entry.IsSummary` into a column of
its own when you append, and declare the method in your store to start at the last row that has it:

<!-- snippet: ef-core-conversations -->
<a id='snippet-ef-core-conversations'></a>
```cs
public async Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(int conversation, CancellationToken ct)
{
    await using var db = await contexts.CreateDbContextAsync(ct);

    var rows = await db.Entries
        .Where(entry => entry.Conversation == conversation)
        .OrderBy(entry => entry.Sequence)
        .ToListAsync(ct);

    return [.. rows.Select(row => ConversationEntry.Deserialize(row.Json))];
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

    return [.. rows.Select(row => ConversationEntry.Deserialize(row.Json))];
}

public async Task AppendConversationAsync(int conversation, ConversationEntry entry, CancellationToken ct)
{
    await using var db = await contexts.CreateDbContextAsync(ct);

    db.Entries.Add(new EntryRow
    {
        Conversation = conversation,
        IsSummary = entry.IsSummary,
        Json = entry.Serialize()
    });

    await db.SaveChangesAsync(ct);
}
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Stores/EfCore/EfTypeshiftStore.cs#L52-L99' title='Snippet source file'>snippet source</a> | <a href='#snippet-ef-core-conversations' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`LoadConversationAsync` still returns every entry, summaries included, for your app's own use, such as showing the whole
history. Runs call it too in one case: when the last summary has no `StandingApprovals`, as one stored by a Typeshift
version before 0.1.0-alpha.3, they read the whole conversation. Compaction always fills them in, even as an empty list,
so set them on any summary you build for a test of your own, as [`CheckConversationsAsync`](#check-your-store) does.

## Check your store

`StoreCheck` runs this page's rules for keys, revisions and conversation entries against your store. Call
`CheckSettingsAsync` for an `ISettingsStore` and `CheckConversationsAsync` for an `IConversationStore` (both for a
store that is both), from a test of your own in any test framework, with your store over a test database:

<!-- snippet: store-check -->
<a id='snippet-store-check'></a>
```cs
var store = new EfTypeshiftStore(contexts);
var settingsId = 9001;
var conversationId = 9001;

await StoreCheck.CheckSettingsAsync(store, settingsId);
await StoreCheck.CheckConversationsAsync(store, conversationId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/StoreChecks.cs#L13-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-store-check' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- A broken rule throws an `InvalidOperationException` whose message names the rule and what to change, such as
  keying documents by id and kind together. An exception your store throws itself passes through as it is, except
  where a `StoreConflictException` belongs: there the message names the type your store threw instead.
- `CheckSettingsAsync` saves a document, then one of another kind under the same id, and loads both back. It then
  saves the first document at revision 0, and with its old revision after a save with the current one. Those two
  saves must throw a `StoreConflictException` and leave the document as it was. Every save that succeeds must return
  a new revision other than 0.
- `CheckConversationsAsync` appends a run's input and its end, then two summaries with `IsSummary` and
  `StandingApprovals` set, each followed by a run's end. After the first run and again at the end, it loads the
  conversation whole and from the last summary on, which is every entry while there is no summary. Each entry must
  come back unchanged, in the order it was appended.
- Both write under the id you pass, so calling them twice with one id fails with a message that asks for a new id.
  Give each call an id nothing else uses, such as a new `Guid`, or start from an empty database, as the docs sample
  does.
- They make one call at a time, so they cannot catch two saves that race. A store that reads the revision and then
  writes passes them and still loses saves under load ([What a save must do](#what-a-save-must-do)).

## Related

- [Settings and stores](./settings-and-stores.md): how stored settings layer over the agent's code, and the JSON a
  store keeps.
- [Conversations](./conversations.md): what a run stores, resuming, and compaction.
- [Registration](./registration.md#the-chain): `WithStore<T>()` among the other chain calls.
- [Generated members](./generated-members.md#settings): the `Settings` record a store keeps, with its `Kind` and
  `Version`.
