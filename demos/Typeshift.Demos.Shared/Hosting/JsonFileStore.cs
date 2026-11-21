using System.Text.Json;

namespace Typeshift.Demos.Hosting;

/// <summary>
/// A store on disk, for demos that survive a restart: documents as one JSON file each under their kind, conversations
/// as one file of JSON lines each, appended as the run goes, and each conversation's decisions as one JSON file.
/// </summary>
public sealed class JsonFileStore(string directory, TimeProvider time) : ITypeshiftStore<Guid>
{
    // How long an append waits for another process's append to the same conversation, in tries and the pause between
    private const int LockAttempts = 200;

    private static readonly TimeSpan LockPause = TimeSpan.FromMilliseconds(10);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // One file operation at a time keeps versions honest and no read meets a write in this process. Only an append to
    // a conversation is guarded against a second process, by its lock file
    private readonly SemaphoreSlim _files = new(1, 1);

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken cancellationToken) =>
        LoadDocumentAsync(DocumentPath(kind, id), cancellationToken);

    public Task<long> SaveAsync(
        Guid id,
        string kind,
        string json,
        long loadedVersion,
        CancellationToken cancellationToken) =>
        SaveDocumentAsync(DocumentPath(kind, id), $"{kind} {id}", json, loadedVersion, cancellationToken);

    public Task<StoredDocument?> LoadDecisionsAsync(Guid conversation, CancellationToken cancellationToken) =>
        LoadDocumentAsync(DecisionsPath(conversation), cancellationToken);

    public Task<long> SaveDecisionsAsync(
        Guid conversation,
        string json,
        long loadedVersion,
        CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            DecisionsPath(conversation),
            $"The decisions of {conversation}",
            json,
            loadedVersion,
            cancellationToken);

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(
        Guid conversation,
        CancellationToken cancellationToken)
    {
        await _files.WaitAsync(cancellationToken);

        try
        {
            return await ReadEntriesAsync(ConversationPath(conversation), cancellationToken);
        }
        finally
        {
            _files.Release();
        }
    }

    // A file of lines reads whole, so the entries from the last summary on are kept after the read
    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        (await LoadConversationAsync(conversation, cancellationToken)).SinceLastSummary();

    // Of two runs that loaded a conversation at the same point, the one that appends second is refused, as a unique
    // key on the conversation and the number would refuse it, also in another process: the number is checked and the
    // line written while the conversation's lock file is open with FileShare.None. On a local disk that keeps out the
    // append of every process using this store, as Windows refuses a second open and .NET takes an advisory lock on
    // Linux and macOS. A program that writes the file without that lock, or a network share that keeps no locks, can
    // still slip an entry in between
    public async Task AppendConversationAsync(
        Guid conversation,
        ConversationEntry entry,
        CancellationToken cancellationToken)
    {
        var path = ConversationPath(conversation);
        var line = System.Text.Encoding.UTF8.GetBytes(entry.Serialize() + "\n");
        await _files.WaitAsync(cancellationToken);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var held = await LockAsync($"{path}.lock", cancellationToken);

            // A demo's conversations are short, so the file is read whole to find the number
            var entries = entry.Sequence > 0 ? await ReadEntriesAsync(path, cancellationToken) : [];

            if (entries.Any(stored => stored.Sequence == entry.Sequence))
                throw new StoreConflictException($"Conversation {conversation} holds entry {entry.Sequence} already.");

            await using var file = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read,
                bufferSize: 1);

            DropTornLine(file);
            await file.WriteAsync(line, cancellationToken);
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
                break;
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

    // Another process holds the lock while it appends to the conversation, so this one tries again after a pause,
    // and gives up with the last IOException after about two seconds. The file stays: a process that waits for it
    // would otherwise lock a file deleted under it
    private async Task<FileStream> LockAsync(string path, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (attempt < LockAttempts)
            {
                await Task.Delay(LockPause, time, ct);
            }
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
