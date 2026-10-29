using Microsoft.EntityFrameworkCore;

namespace Typeshift.Samples.Stores.EfCore;

// begin-snippet: ef-core-load
internal sealed class EfTypeshiftStore(IDbContextFactory<TypeshiftDb> contexts) : ITypeshiftStore
{
    public async Task<StoredDocument?> LoadAsync(int id, string kind, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        return await db.Documents.FindAsync([id, kind], ct) is { } row
            ? new StoredDocument(row.Json, row.Version)
            : null;
    }
    // end-snippet

    // begin-snippet: ef-core-save
    public async Task<long> SaveAsync(int id, string kind, string json, long expectedVersion, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        var row = new DocumentRow
        {
            Id = id,
            Kind = kind,
            Version = expectedVersion + 1,
            Json = json
        };

        var entry = db.Entry(row);
        entry.State = expectedVersion == 0 ? EntityState.Added : EntityState.Modified;

        // The update only matches the row while it still has the version the caller loaded
        if (expectedVersion > 0)
            entry.Property(document => document.Version).OriginalValue = expectedVersion;

        // A conflict is another save that won; any other failure is the database's and surfaces as it is
        try
        {
            await db.SaveChangesAsync(ct);
            return row.Version;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new StoreConflictException($"{kind} {id} changed after version {expectedVersion}.", exception);
        }
        catch (DbUpdateException exception) when (expectedVersion == 0)
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
    // end-snippet

    // begin-snippet: ef-core-conversations
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
// end-snippet
