using Typeshift.Persistence;
using Typeshift.Samples.Docs.Agents;
using Microsoft.EntityFrameworkCore;

namespace Typeshift.Samples.Docs.Stores.EfCore;

// begin-snippet: ef-core-store
internal sealed class EfTypeshiftStore(IDbContextFactory<TypeshiftDbContext> contexts) : ITypeshiftStore<string>
{
    public async Task<TData?> LoadAsync<TData>(string tenant, CancellationToken ct)
        where TData : class, IStorable<TData>
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        // The support agent's settings live in columns of their own, everything else in JSON documents
        if (typeof(TData) == typeof(SupportAgent.Settings))
            return await db.SupportAgentSettings.FindAsync([tenant], ct) is { } row
                ? (TData)(object)row.ToSettings()
                : null;

        return await db.Documents.FindAsync([Kind<TData>(), tenant], ct) is { } document
            ? TData.Deserialize(document.Json)
            : null;
    }

    public async Task SaveAsync<TData>(string tenant, TData data, CancellationToken ct)
        where TData : class, IStorable<TData>
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        if (data is SupportAgent.Settings settings)
        {
            var row = SupportAgentSettingsRow.From(tenant, settings);
            Upsert(db, await db.SupportAgentSettings.FindAsync([tenant], ct), row);
        }
        else
        {
            var document = new StoredDocument
            {
                Kind = Kind<TData>(),
                Id = tenant,
                Json = data.Serialize()
            };

            Upsert(db, await db.Documents.FindAsync([document.Kind, tenant], ct), document);
        }

        await db.SaveChangesAsync(ct);
    }

    // The full name keeps the Settings of two agents apart
    private static string Kind<TData>() => typeof(TData).FullName!;

    private static void Upsert<TRow>(TypeshiftDbContext db, TRow? existing, TRow row) where TRow : class
    {
        if (existing is null) db.Add(row);
        else db.Entry(existing).CurrentValues.SetValues(row);
    }
}
// end-snippet
