using Microsoft.EntityFrameworkCore;

namespace Typeshift.Samples.Stores.EfCore;

// begin-snippet: ef-core-db
internal sealed class TypeshiftDb(DbContextOptions<TypeshiftDb> options) : DbContext(options)
{
    public DbSet<AtomRow> Atoms => Set<AtomRow>();

    public DbSet<EntryRow> Entries => Set<EntryRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AtomRow>()
            .HasKey(atom => new { atom.Id, atom.Kind });

        model.Entity<AtomRow>()
            .Property(atom => atom.Revision)
            .IsConcurrencyToken();

        model.Entity<EntryRow>()
            .HasKey(entry => entry.Sequence);

        model.Entity<EntryRow>()
            .HasIndex(entry => entry.Conversation);
    }
}

internal sealed class AtomRow
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
// end-snippet
