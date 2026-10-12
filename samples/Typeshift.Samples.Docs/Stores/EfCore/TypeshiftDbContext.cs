using Microsoft.EntityFrameworkCore;

namespace Typeshift.Samples.Docs.Stores.EfCore;

internal sealed class TypeshiftDbContext(DbContextOptions<TypeshiftDbContext> options) : DbContext(options)
{
    public DbSet<StoredDocument> Documents => Set<StoredDocument>();

    public DbSet<SupportAgentSettingsRow> SupportAgentSettings => Set<SupportAgentSettingsRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<StoredDocument>().HasKey(document => new { document.Kind, document.Id });
        model.Entity<SupportAgentSettingsRow>().HasKey(row => row.Tenant);
    }
}
