using Microsoft.EntityFrameworkCore;

namespace Typeshift.Samples.Stores.EfCore;

// begin-snippet: ef-core-db
internal sealed class TypeshiftDb(DbContextOptions<TypeshiftDb> options) : DbContext(options)
{
    public DbSet<DocumentRow> Documents => Set<DocumentRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<DocumentRow>().HasKey(document => new { document.Kind, document.Id });
        model.Entity<DocumentRow>().Property(document => document.Revision).IsConcurrencyToken();
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
// end-snippet
