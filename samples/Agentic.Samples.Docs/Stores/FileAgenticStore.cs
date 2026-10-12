using Agentic.Persistence;
using Microsoft.Extensions.Options;

namespace Agentic.Samples.Docs.Stores;

// begin-snippet: file-store
internal sealed class FileAgenticStore(IOptions<FileStoreOptions> options) : IAgenticStore<Guid>
{
    public async Task<TData?> LoadAsync<TData>(Guid id, CancellationToken ct) where TData : class, IStorable<TData>
    {
        var path = GetPath<TData>(id);

        return File.Exists(path) ? TData.Deserialize(await File.ReadAllTextAsync(path, ct)) : null;
    }

    public async Task SaveAsync<TData>(Guid id, TData data, CancellationToken ct) where TData : class, IStorable<TData>
    {
        var path = GetPath<TData>(id);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, data.Serialize(), ct);
    }

    // One folder per kind of data, one file per id; the full name keeps the Settings of two agents apart
    private string GetPath<TData>(Guid id) => Path.Combine(options.Value.Root, typeof(TData).FullName!, $"{id}.json");
}

internal sealed class FileStoreOptions
{
    public string Root { get; set; } = "data";
}
// end-snippet
