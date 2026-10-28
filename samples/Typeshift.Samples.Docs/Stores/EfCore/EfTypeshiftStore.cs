using Microsoft.EntityFrameworkCore;

namespace Typeshift.Samples.Stores.EfCore;

// begin-snippet: ef-core-store
internal sealed class EfTypeshiftStore(IDbContextFactory<TypeshiftDb> contexts) : ITypeshiftStore
{
    public async Task<Atom?> LoadAtomAsync(int id, string kind, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        return await db.Atoms.FindAsync([id, kind], ct) is { } row
            ? new Atom(row.Json, row.Version, row.Revision)
            : null;
    }

    public async Task<long> SaveAtomAsync(
        int id,
        string kind,
        string json,
        int version,
        long revision,
        CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        var row = new AtomRow
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
            entry.Property(atom => atom.Revision).OriginalValue = revision;

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
        return await db.Atoms.AnyAsync(row => row.Id == id && row.Kind == kind, ct);
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
// end-snippet
