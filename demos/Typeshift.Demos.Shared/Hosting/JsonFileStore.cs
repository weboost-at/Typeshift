using System.Text.Json;

namespace Typeshift.Demos.Hosting;

/// <summary>
/// A store on disk, for demos that survive a restart: documents as one JSON file each under their kind, conversations
/// as one file of JSON lines each, appended as the run goes.
/// </summary>
public sealed class JsonFileStore(string directory) : ITypeshiftStore<Guid>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // One writer at a time keeps revisions honest; a demo has no second process to guard against
    private readonly SemaphoreSlim _writing = new(1, 1);

    public async Task<Stored<TData>?> LoadAsync<TData>(Guid id, CancellationToken ct)
        where TData : class, IStorable<TData>
    {
        var path = DocumentPath(TData.Kind, id);
        if (!File.Exists(path)) return null;

        var row = JsonSerializer.Deserialize<DocumentRow>(await File.ReadAllTextAsync(path, ct), Json)!;
        return new Stored<TData>(TData.Deserialize(row.Json, row.Version), row.Revision);
    }

    public async Task<long> SaveAsync<TData>(Guid id, TData data, long revision, CancellationToken ct)
        where TData : class, IStorable<TData>
    {
        var path = DocumentPath(TData.Kind, id);
        await _writing.WaitAsync(ct);

        try
        {
            var current = File.Exists(path)
                ? JsonSerializer.Deserialize<DocumentRow>(await File.ReadAllTextAsync(path, ct), Json)!.Revision
                : 0;

            if (current != revision)
                throw new StoreConflictException($"{TData.Kind} {id} is at revision {current}, not {revision}.");

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var row = new DocumentRow(TData.Version, revision + 1, data.Serialize());
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(row, Json), ct);
            return row.Revision;
        }
        finally
        {
            _writing.Release();
        }
    }

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct)
    {
        var path = ConversationPath(conversation);
        if (!File.Exists(path)) return [];

        var lines = await File.ReadAllLinesAsync(path, ct);

        return
        [
            .. lines
                .Where(line => line.Length > 0)
                .Select(line => JsonSerializer.Deserialize<EntryRow>(line, Json)!)
                .Select(row => ConversationEntry.Deserialize(row.Json, row.Version))
        ];
    }

    public async Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct)
    {
        var path = ConversationPath(conversation);
        var line = JsonSerializer.Serialize(new EntryRow(ConversationEntry.Version, entry.Serialize()), Json);
        await _writing.WaitAsync(ct);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.AppendAllTextAsync(path, line + Environment.NewLine, ct);
        }
        finally
        {
            _writing.Release();
        }
    }

    public IEnumerable<Guid> Conversations() =>
        Directory.Exists(Path.Combine(directory, "conversations"))
            ? Directory.EnumerateFiles(Path.Combine(directory, "conversations"), "*.jsonl")
                .Select(file => Guid.Parse(Path.GetFileNameWithoutExtension(file)))
            : [];

    private string DocumentPath(string kind, Guid id) => Path.Combine(directory, "documents", kind, $"{id}.json");

    private string ConversationPath(Guid conversation) =>
        Path.Combine(directory, "conversations", $"{conversation}.jsonl");

    private sealed record DocumentRow(int Version, long Revision, string Json);

    private sealed record EntryRow(int Version, string Json);
}
