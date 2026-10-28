---
title: Implementing a store
description: Write the ITypeshiftStore that keeps your agents' settings and conversations in your own database, from its lifetime and revisions to conversation summaries.
---

# Implementing a store

Typeshift keeps every agent's settings and every conversation through one store, an `ITypeshiftStore` you implement
over your own database. This page walks through what a store must do, in the order you meet it: its lifetime, the
settings it saves with their revisions, and the conversations it appends to. `InMemoryTypeshiftStore` is for tests
and samples, so a production app writes its own.

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

`ITypeshiftStore` has five methods. You implement four of them, and `LoadConversationSinceCompactionAsync` has a
default you may replace by declaring the method in your store. Every id has your build's
[id type](./registration.md#the-id-type), which is `Guid` unless `TypeshiftIdType` names another; the samples on this
page set it to `int`. Each method takes a `CancellationToken` last.

| Method | Does |
|---|---|
| `LoadAsync(id, kind)` | Returns the document of a kind under an id as a `StoredDocument`, with its JSON, version and revision, or null when none is stored. |
| `SaveAsync(id, kind, json, version, revision)` | Saves a document's JSON and version if its stored revision still equals `revision` (0 when nothing is stored yet), and returns the new revision. Throws a `StoreConflictException` when another save came first. |
| `LoadConversationAsync(conversation)` | Returns a conversation's entries, oldest first. |
| `LoadConversationSinceCompactionAsync(conversation)` | Returns the conversation's last summary and the entries after it ([Recognizing a summary](#recognizing-a-summary)). |
| `AppendConversationAsync(conversation, entry)` | Appends one entry to a conversation. The first entry creates the conversation. |

The first two methods keep settings and the other three keep conversations, so the database on this page has a table
for each:

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
    public required int Id { get; init; }

    public required string Kind { get; init; }

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

## Settings documents

A document is an agent's `Settings` record, the one `ConfigureAsync` and `SaveSettingsAsync` save. Typeshift writes
the record as JSON before it calls `SaveAsync` and reads it back after `LoadAsync`, so your store handles strings and
numbers only. Keep each document in a row keyed by the id and the kind, with the JSON, the version and the
[revision](#revisions):

- **The id** is the settings id your app passed, such as a customer's.
- **The kind** is the agent's class name, or what its `WithStorageKind("...")` sets. Under one settings id, each agent
  has a document of its own, so the id and the kind together are the key.
- **The JSON** is the record as Typeshift wrote it ([The stored JSON](./settings-and-stores.md#the-stored-json)).
  Keep it as text and return it unchanged.
- **The version** is the schema version the agent's `WithSettingsVersion(n)` sets, 1 unless raised. Store it with the
  JSON and return it with the JSON.

Typeshift refuses a version newer than the code's, so an app rolled back to older code fails loudly instead of
reading settings it does not understand. An agent raises its version when a configurable member is renamed or
removed, and migrating the stored JSON of older versions is then your store's job. `LoadAsync` is a good place for
it: the kind tells you which agent's settings a row holds, so you rewrite the JSON of an older version and return it
with the version it now has and the revision the row holds. Each save writes the current version, so a migrated
document reaches the new version in your database with its next save.

Loading reads the row by its id and kind and returns its JSON, version and revision as a `StoredDocument`:

<!-- snippet: ef-core-load -->
<a id='snippet-ef-core-load'></a>
```cs
internal sealed class EfTypeshiftStore(IDbContextFactory<TypeshiftDb> contexts) : ITypeshiftStore
{
    public async Task<StoredDocument?> LoadAsync(int id, string kind, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        return await db.Documents.FindAsync([id, kind], ct) is { } row
            ? new StoredDocument(row.Json, row.Version, row.Revision)
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

1. **Revision 0 means the caller found nothing.** Insert the document, and throw a `StoreConflictException` when one
   exists already, because another save created it in between.
2. **Any other revision is the one the caller loaded.** Update the document only while its stored revision still
   equals it, and throw a `StoreConflictException` otherwise, also when the document is gone.
3. **Check and write in one step.** In SQL, that is
   `UPDATE ... SET ..., revision = @revision + 1 WHERE id = @id AND kind = @kind AND revision = @revision`, and no
   updated row means a conflict; an insert that hits the primary key is one too. A concurrency token or a transaction
   does the same. A read followed by a separate write lets two saves of the same revision both win.
4. **Return the new revision.** Any number works as long as it is not 0 and was never this document's revision
   before; counting up by one is the simple choice.
5. **Throw `StoreConflictException` only for a lost race**, with a message that names the document and the database's
   exception as the inner one when you have it. Any other failure, such as a lost connection, goes to the caller as
   it is.

The EF Core store maps these rules onto a concurrency token. The update matches the row only while it still holds the
loaded revision, and an insert that finds the row taken is a conflict too:

<!-- snippet: ef-core-save -->
<a id='snippet-ef-core-save'></a>
```cs
public async Task<long> SaveAsync(
    int id,
    string kind,
    string json,
    int version,
    long revision,
    CancellationToken ct)
{
    await using var db = await contexts.CreateDbContextAsync(ct);

    var row = new DocumentRow
    {
        Id = id,
        Kind = kind,
        Version = version,
        Revision = revision + 1,
        Json = json
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
        throw new StoreConflictException($"{kind} {id} changed after revision {revision}.", exception);
    }
    catch (DbUpdateException exception) when (revision == 0)
    {
        if (!await ExistsAsync(id, kind, ct)) throw;

        throw new StoreConflictException($"{kind} {id} was saved first by another.", exception);
    }
}

private async Task<bool> ExistsAsync(int id, string kind, CancellationToken ct)
{
    await using var db = await contexts.CreateDbContextAsync(ct);
    return await db.Documents.AnyAsync(row => row.Id == id && row.Kind == kind, ct);
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Stores/EfCore/EfTypeshiftStore.cs#L18-L68' title='Snippet source file'>snippet source</a> | <a href='#snippet-ef-core-save' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Who saves

Runs only load settings; they never save them. Saves come from your app:

- `ConfigureAsync` loads the settings, applies your change and saves them with the loaded revision. On a conflict it
  loads and applies the change again, up to ten times, then throws the last `StoreConflictException`, so even a store
  that conflicts on every save fails after ten tries.
- `SaveSettingsAsync` saves with the revision you pass, usually the one `LoadSettingsAsync` returned to a settings
  form. On a conflict, load the settings again and show the user the current values
  ([Agent settings](./settings-and-stores.md#agent-settings)).

## Conversations

A conversation only grows. Runs append each step as it happens: the input, every model response, the tool results
of each round of calls, steering input and the entry that ends a run
([What is stored](./conversations.md#what-is-stored)). Your store never changes or removes an entry:

- **Keep the append order.** `LoadConversationAsync` returns the entries in the order they were appended, oldest
  first. Let the database number the rows, as an identity column does; a timestamp can tie.
- **Store each entry as its JSON**, `entry.Serialize()`, with `ConversationEntry.Version` next to it, and read it back
  with `ConversationEntry.Deserialize(json, version)`, passing the stored version. A database client that serializes
  with `System.Text.Json` or Newtonsoft.Json may take the entry as it is
  ([What is stored](./conversations.md#what-is-stored)), but those libraries read every entry as the current version,
  so keep the version next to it all the same.
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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Stores/EfCore/EfTypeshiftStore.cs#L70-L118' title='Snippet source file'>snippet source</a> | <a href='#snippet-ef-core-conversations' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`LoadConversationAsync` still returns every entry, summaries included, for your app's own use, such as showing the
whole history. Runs call it too in one case: when the last summary has no `StandingApprovals`, as one stored by a
Typeshift version before 0.1.0-alpha.3, they read the whole conversation. Compaction always fills them in, even as
an empty list, so test your store with a summary compaction wrote, for example by giving an agent a small
`WithMaxInputTokens`, rather than with an entry you built yourself.

## Related

- [Settings and stores](./settings-and-stores.md): how stored settings layer over the agent's code, and the JSON a
  store keeps.
- [Conversations](./conversations.md): what a run stores, resuming, and compaction.
- [Registration](./registration.md#the-chain): `WithStore<T>()` among the other chain calls.
- [Generated members](./generated-members.md#settings): the `Settings` record a store keeps, with its `Kind` and
  `Version`.
