using Microsoft.EntityFrameworkCore;

namespace Typeshift.Samples.Stores.EfCore;

// begin-snippet: ef-core-db
internal sealed class TypeshiftDb(DbContextOptions<TypeshiftDb> options) : DbContext(options)
{
    public DbSet<DocumentRow> Documents => Set<DocumentRow>();

    public DbSet<EntryRow> Entries => Set<EntryRow>();

    public DbSet<DecisionsRow> Decisions => Set<DecisionsRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<DocumentRow>()
            .HasKey(document => new { document.Id, document.Kind });

        model.Entity<EntryRow>()
            .HasKey(entry => entry.Sequence);

        model.Entity<EntryRow>()
            .HasIndex(entry => entry.Conversation);

        model.Entity<DecisionsRow>()
            .HasKey(decisions => decisions.Conversation);

        // The application sets the key, the conversation's id, so the database must not generate it
        model.Entity<DecisionsRow>()
            .Property(decisions => decisions.Conversation)
            .ValueGeneratedNever();
    }
}

internal sealed class DocumentRow
{
    public required int Id { get; init; }

    public required string Kind { get; init; }

    public required long Version { get; init; }

    public required string Json { get; init; }
}

internal sealed class EntryRow
{
    public long Sequence { get; init; }

    public required int Conversation { get; init; }

    public required bool IsSummary { get; init; }

    public required string Json { get; init; }
}

internal sealed class DecisionsRow
{
    public required int Conversation { get; init; }

    public required long Version { get; init; }

    public required string Json { get; init; }
}
// end-snippet
