using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Typeshift.Generated;

namespace Typeshift;

// The settings part of the file store, which WithFileStore registers over a folder it resolved
// D67: a save holds the kind's lock file, writes a file of its own beside the old one and moves it over, so a load
// reads the old file or the new one whole and two saves never pass the same version
internal sealed class FileAgentSettingsStore : IAgentSettingsStore
{
    private const string Shape = "{\"kind\": \"...\", \"version\": n, \"settings\": {...}}, with n from 1";

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    // How long a save waits for the kind's lock file before it fails
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(30);

    private readonly string _directory;

    private readonly TimeProvider _time;

    private readonly ILogger _logger;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _saves = [];

    private int _announced;

    // The folder is absolute, and the first save creates it when it is missing
    public FileAgentSettingsStore(string directory, TimeProvider? time = null, ILogger? logger = null)
    {
        _directory = directory;
#pragma warning disable RS0030 // the default for an app that passes no TimeProvider
        _time = time ?? TimeProvider.System;
#pragma warning restore RS0030
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task<StoredDocument?> LoadAsync(string kind, CancellationToken ct)
    {
        var path = PathOf(kind);
        return await TryReadAsync(path, ct) is { } file ? Read(path, kind, file) : null;
    }

    public async Task<long> SaveAsync(string kind, string json, long loadedVersion, CancellationToken ct)
    {
        var path = PathOf(kind);
        var saves = _saves.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await saves.WaitAsync(ct);

        try
        {
            Directory.CreateDirectory(_directory);

            // D67: the default folder lies in the app's output, which a new build or deployment leaves behind, so
            // the first save says where the settings are
            if (Interlocked.Exchange(ref _announced, 1) == 0) LogEvents.FileStoreFolder(_logger, _directory);

            await using var held = await LockAsync($"{path}.lock", ct);
            DeleteUnfinished(path);

            var stored = await TryReadAsync(path, ct) is { } file ? Read(path, kind, file).Version : 0;

            if (stored != loadedVersion)
                throw new StoreConflictException($"The agent's {kind} was saved since version {loadedVersion}.");

            await ReplaceAsync(path, Write(kind, json, loadedVersion + 1), ct);
            return loadedVersion + 1;
        }
        finally
        {
            saves.Release();
        }
    }

    // A file of its own, flushed to the disk before it takes the place of the old one, and gone if the save fails
    private static async Task ReplaceAsync(string path, byte[] content, CancellationToken ct)
    {
        var written = $"{path}.{Guid.NewGuid():N}.saving";

        try
        {
            await using (var file = new FileStream(written, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.WriteAsync(content, ct);
                file.Flush(flushToDisk: true);
            }

            File.Move(written, path, overwrite: true);
        }
        catch
        {
            File.Delete(written);
            throw;
        }
    }

    // A save writes its file under the kind's lock and removes it before it lets go, so a file found under the lock is
    // one of a save that died
    private void DeleteUnfinished(string path)
    {
        foreach (var unfinished in Directory.EnumerateFiles(_directory, $"{Path.GetFileName(path)}.*.saving"))
        {
            File.Delete(unfinished);
        }
    }

    // FileShare.None is an exclusive lock on Unix and a sharing mode on Windows, so it keeps out other stores and
    // processes as well. The wait ends, on the app's clock, so a lock that a hung program holds fails the save
    // instead of hanging it
    private async Task<FileStream> LockAsync(string path, CancellationToken ct)
    {
        var started = _time.GetTimestamp();
        var waiting = false;

        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception) when (File.Exists(path))
            {
                if (_time.GetElapsedTime(started) >= LockWait)
                {
                    throw new TimeoutException(
                        $"{path} has been held by another save or program for {LockWait.TotalSeconds:0} seconds, so "
                        + "the settings were not saved.",
                        exception);
                }

                if (!waiting) LogEvents.FileStoreWaiting(_logger, path);
                waiting = true;

                await Task.Delay(TimeSpan.FromMilliseconds(5), _time, ct);
            }
        }
    }

    // A load may read while a save moves the new file over this one, which Windows allows only to a reader that shares
    // deletion; a missing file or folder means nothing is stored, any other failure is the caller's to see
    private static async Task<string?> TryReadAsync(string path, CancellationToken ct)
    {
        try
        {
            await using var file = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true);

            using var reader = new StreamReader(file, Encoding.UTF8);
            return await reader.ReadToEndAsync(ct);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    // Any kind maps to a file of the folder, one to one: letters, digits, '_' and '-' stay, every other byte of its
    // UTF-8 becomes %XX, and a name Windows reserves gets its first letter escaped
    private string PathOf(string kind)
    {
        ArgumentException.ThrowIfNullOrEmpty(kind);
        var name = new StringBuilder();

        foreach (var value in Encoding.UTF8.GetBytes(kind))
        {
            if (char.IsAsciiLetterOrDigit((char)value) || value is (byte)'_' or (byte)'-') name.Append((char)value);
            else name.Append('%').Append(value.ToString("X2"));
        }

        if (Reserved.Contains(name.ToString())) name.Remove(0, 1).Insert(0, $"%{(byte)kind[0]:X2}");

        // NAME_MAX is 255 bytes, and a save writes a name 40 longer
        return name.Length <= 200
            ? Path.Combine(_directory, $"{name}.json")
            : throw new ArgumentException(
                $"The kind '{kind}' makes a file name too long: give the agent a shorter WithStorageKind.",
                nameof(kind));
    }

    // The settings as Typeshift wrote them, under the version a save must name; a file of another kind is one that a
    // file system ignoring case shares between two kinds
    private static StoredDocument Read(string path, string kind, string file)
    {
        try
        {
            using var document = JsonDocument.Parse(file);
            var root = document.RootElement;

            // A member of another JSON type than the shape's, in a file edited by hand, reads as missing
            var stored = StorableJson.Member(root, "kind");
            var version = StorableJson.Member(root, "version");
            var settings = StorableJson.Member(root, "settings");

            if (stored.ValueKind is JsonValueKind.String && stored.GetString() != kind)
                throw new InvalidDataException(
                    $"{path} holds the settings of '{stored.GetString()}', not of '{kind}': the file system ignores "
                    + "case, so give one of the agents a WithStorageKind of its own.");

            return stored.ValueKind is JsonValueKind.String or JsonValueKind.Undefined
                && version.ValueKind is JsonValueKind.Number
                && version.TryGetInt64(out var number)
                && number > 0
                && settings.ValueKind is JsonValueKind.Object
                    ? new StoredDocument(settings.GetRawText(), number)
                    : throw new InvalidDataException($"{path} is no settings file: it holds {Shape}.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{path} is no JSON: it holds {Shape}.", exception);
        }
    }

    private static byte[] Write(string kind, string json, long version)
    {
        using var stream = new MemoryStream();

        var options = new JsonWriterOptions
        {
            Indented = true,
            Encoder = StorableJson.Encoder
        };

        using (var writer = new Utf8JsonWriter(stream, options))
        {
            writer.WriteStartObject();
            writer.WriteString("kind", kind);
            writer.WriteNumber("version", version);
            writer.WritePropertyName("settings");
            writer.WriteRawValue(json);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }
}
