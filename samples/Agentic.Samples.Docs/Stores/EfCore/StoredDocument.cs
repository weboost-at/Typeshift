namespace Agentic.Samples.Docs.Stores.EfCore;

internal sealed class StoredDocument
{
    public required string Kind { get; init; }

    public required string Id { get; init; }

    public required string Json { get; set; }
}
