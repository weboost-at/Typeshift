using System.Text.Json;

namespace Typeshift.Demos.Hosting;

/// <summary>
/// A store on disk, for demos that survive a restart: documents as one JSON file each under their kind, conversations
/// as one file of JSON lines each, appended as the run goes, and each conversation's decisions as one JSON file.
/// </summary>
public sealed class JsonFileStore(string directory) : ITypeshiftStore<Guid>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // One writer at a time keeps versions honest; a demo has no second process to guard against
    private readonly SemaphoreSlim _writing = new(1, 1);

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        LoadDocumentAsync(DocumentPath(kind, id), ct);

    public Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct) =>
        SaveDocumentAsync(DocumentPath(kind, id), $"{kind} {id}", json, loadedVersion, ct);

    public Task<StoredDocument?> LoadDecisionsAsync(Guid conversation, CancellationToken ct) =>
        LoadDocumentAsync(DecisionsPath(conversation), ct);

    public Task<long> SaveDecisionsAsync(Guid conversation, string json, long loadedVersion, CancellationToken ct) =>
        SaveDocumentAsync(DecisionsPath(conversation), $"The decisions of {conversation}", json, loadedVersion, ct);

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct)
    {
        var path = ConversationPath(conversation);
        if (!File.Exists(path)) return [];

        var lines = await File.ReadAllLinesAsync(path, ct);

        return
        [
            .. lines
                .Where(line => line.Length > 0)
                .Select(ConversationEntry.Deserialize)
        ];
    }

    // A file of lines reads whole, so the entries from the last summary on are kept after the read
    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct) =>
        (await LoadConversationAsync(conversation, ct)).SinceLastSummary();

    public async Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct)
    {
        var path = ConversationPath(conversation);
        var line = entry.Serialize();
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

    private static async Task<StoredDocument?> LoadDocumentAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return null;

        var row = JsonSerializer.Deserialize<DocumentRow>(await File.ReadAllTextAsync(path, ct), Json)!;
        return new StoredDocument(row.Json, row.Version);
    }

    private async Task<long> SaveDocumentAsync(
        string path,
        string document,
        string json,
        long loadedVersion,
        CancellationToken ct)
    {
        await _writing.WaitAsync(ct);

        try
        {
            var current = File.Exists(path)
                ? JsonSerializer.Deserialize<DocumentRow>(await File.ReadAllTextAsync(path, ct), Json)!.Version
                : 0;

            if (current != loadedVersion)
                throw new StoreConflictException($"{document} is at version {current}, not {loadedVersion}.");

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var row = new DocumentRow(loadedVersion + 1, json);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(row, Json), ct);
            return row.Version;
        }
        finally
        {
            _writing.Release();
        }
    }

    private string DocumentPath(string kind, Guid id) => Path.Combine(directory, "documents", kind, $"{id}.json");

    private string DecisionsPath(Guid conversation) => Path.Combine(directory, "decisions", $"{conversation}.json");

    private string ConversationPath(Guid conversation) =>
        Path.Combine(directory, "conversations", $"{conversation}.jsonl");

    private sealed record DocumentRow(long Version, string Json);
}
