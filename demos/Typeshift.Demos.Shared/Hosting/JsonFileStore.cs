using System.Text.Json;

namespace Typeshift.Demos.Hosting;

/// <summary>
/// A store on disk, for demos that survive a restart: documents as one JSON file each under their kind, conversations
/// as one file of JSON lines each, appended as the run goes, and each conversation's decisions as one JSON file.
/// </summary>
public sealed class JsonFileStore(string directory) : ITypeshiftStore<Guid>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // One file operation at a time keeps versions honest and no read meets a write; a demo has no second process to
    // guard against
    private readonly SemaphoreSlim _files = new(1, 1);

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
        await _files.WaitAsync(ct);

        try
        {
            return await ReadEntriesAsync(ConversationPath(conversation), ct);
        }
        finally
        {
            _files.Release();
        }
    }

    // A file of lines reads whole, so the entries from the last summary on are kept after the read
    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct) =>
        (await LoadConversationAsync(conversation, ct)).SinceLastSummary();

    // Of two runs that loaded a conversation at the same point, the one that appends second is refused, as a unique
    // key on the conversation and the number would refuse it
    public bool RefusesTakenSequences => true;

    public async Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct)
    {
        var path = ConversationPath(conversation);
        var line = System.Text.Encoding.UTF8.GetBytes(entry.Serialize() + "\n");
        await _files.WaitAsync(ct);

        try
        {
            // A demo's conversations are short, so the file is read whole to find the number
            if (entry.Sequence > 0 && (await ReadEntriesAsync(path, ct)).Any(held => held.Sequence == entry.Sequence))
                throw new StoreConflictException($"Conversation {conversation} holds entry {entry.Sequence} already.");

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var file = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read,
                bufferSize: 1);

            DropTornLine(file);
            await file.WriteAsync(line, ct);
        }
        finally
        {
            _files.Release();
        }
    }

    public IEnumerable<Guid> Conversations() =>
        Directory.Exists(Path.Combine(directory, "conversations"))
            ? Directory.EnumerateFiles(Path.Combine(directory, "conversations"), "*.jsonl")
                .Select(file => Guid.Parse(Path.GetFileNameWithoutExtension(file)))
            : [];

    private static async Task<List<ConversationEntry>> ReadEntriesAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return [];

        string[] lines = [.. (await File.ReadAllLinesAsync(path, ct)).Where(line => line.Length > 0)];
        var entries = new List<ConversationEntry>(lines.Length);

        foreach (var (line, index) in lines.Select((line, index) => (line, index)))
        {
            try
            {
                entries.Add(ConversationEntry.Deserialize(line));
            }
            catch (JsonException) when (index == lines.Length - 1)
            {
                // A crash in the middle of an append cuts the last line off: that entry was never stored
            }
        }

        return entries;
    }

    private async Task<StoredDocument?> LoadDocumentAsync(string path, CancellationToken ct)
    {
        await _files.WaitAsync(ct);

        try
        {
            if (!File.Exists(path)) return null;

            var row = JsonSerializer.Deserialize<DocumentRow>(await File.ReadAllTextAsync(path, ct), Json)!;
            return new StoredDocument(row.Json, row.Version);
        }
        finally
        {
            _files.Release();
        }
    }

    private async Task<long> SaveDocumentAsync(
        string path,
        string document,
        string json,
        long loadedVersion,
        CancellationToken ct)
    {
        await _files.WaitAsync(ct);

        try
        {
            var current = File.Exists(path)
                ? JsonSerializer.Deserialize<DocumentRow>(await File.ReadAllTextAsync(path, ct), Json)!.Version
                : 0;

            if (current != loadedVersion)
                throw new StoreConflictException($"{document} is at version {current}, not {loadedVersion}.");

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var row = new DocumentRow(loadedVersion + 1, json);

            // Written beside it, then moved over it: a crash leaves the old document or the new one, never half
            var written = $"{path}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllTextAsync(written, JsonSerializer.Serialize(row, Json), ct);
            File.Move(written, path, overwrite: true);

            return row.Version;
        }
        finally
        {
            _files.Release();
        }
    }

    // Cuts the file back to its last line break, past what a crash in the middle of an append left; the stream reads
    // unbuffered, a byte at a time from the end
    private static void DropTornLine(FileStream file)
    {
        var end = file.Length;

        while (end > 0)
        {
            file.Position = end - 1;
            if (file.ReadByte() == '\n') break;
            end--;
        }

        file.SetLength(end);
        file.Position = end;
    }

    private string DocumentPath(string kind, Guid id) => Path.Combine(directory, "documents", kind, $"{id}.json");

    private string DecisionsPath(Guid conversation) => Path.Combine(directory, "decisions", $"{conversation}.json");

    private string ConversationPath(Guid conversation) =>
        Path.Combine(directory, "conversations", $"{conversation}.jsonl");

    private sealed record DocumentRow(long Version, string Json);
}
