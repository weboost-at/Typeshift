using Microsoft.EntityFrameworkCore;

namespace Typeshift.Samples.Stores.EfCore;

// begin-snippet: ef-core-load
internal sealed class EfTypeshiftStore(IDbContextFactory<TypeshiftDb> contexts) : ITypeshiftStore
{
    public async Task<StoredDocument?> LoadAsync(int id, string kind, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        return await db.Documents.FindAsync([id, kind], ct) is { } row
            ? new StoredDocument(row.Json, row.Revision)
            : null;
    }
    // end-snippet

    // begin-snippet: ef-core-save
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
