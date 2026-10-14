using Microsoft.EntityFrameworkCore;

namespace Typeshift.Samples.Stores.EfCore;

// begin-snippet: ef-core-store
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

    public async Task<IReadOnlyList<ConversationSession>> LoadConversationAsync(int conversation, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        var rows = await db.Sessions
            .Where(session => session.Conversation == conversation)
            .OrderBy(session => session.Sequence)
            .ToListAsync(ct);

        return [.. rows.Select(row => ConversationSession.Deserialize(row.Json, row.Version))];
    }

    public async Task AppendConversationAsync(int conversation, ConversationSession session, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        db.Sessions.Add(new SessionRow
        {
            Conversation = conversation,
            Version = ConversationSession.Version,
            Json = session.Serialize()
        });

        await db.SaveChangesAsync(ct);
    }
}
// end-snippet
