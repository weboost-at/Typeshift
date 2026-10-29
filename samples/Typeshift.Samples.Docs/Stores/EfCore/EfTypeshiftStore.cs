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
    public async Task<long> SaveAsync(int id, string kind, string json, long loadedVersion, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        var rows = loadedVersion == 0
            ? await InsertAsync(db, id, kind, json, ct)
            : await UpdateAsync(db, id, kind, json, loadedVersion, ct);

        // Each statement checks and writes in one step, so a save that changed no row lost to another save
        return rows == 1
            ? loadedVersion + 1
            : throw new StoreConflictException($"{kind} {id} was saved after version {loadedVersion}.");
    }

    private static Task<int> InsertAsync(TypeshiftDb db, int id, string kind, string json, CancellationToken ct) =>
        db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "Documents" ("Id", "Kind", "Version", "Json")
            VALUES ({id}, {kind}, 1, {json})
            ON CONFLICT ("Id", "Kind") DO NOTHING
            """,
            ct);

    private static Task<int> UpdateAsync(
        TypeshiftDb db,
        int id,
        string kind,
        string json,
        long loadedVersion,
        CancellationToken ct) =>
        db.Documents
            .Where(row => row.Id == id && row.Kind == kind && row.Version == loadedVersion)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(row => row.Json, json)
                    .SetProperty(row => row.Version, loadedVersion + 1),
                ct);
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
