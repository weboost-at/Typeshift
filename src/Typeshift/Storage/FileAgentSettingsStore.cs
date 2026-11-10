using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Typeshift;

/// <summary>
/// Keeps each agent's own settings in a JSON file named after its kind, in a folder: the store of a single-agent app
/// or a local tool that has no database. One process at a time may use the folder.
/// </summary>
// D67: a save writes a file beside the old one and moves it over, so a load reads the old file or the new one whole
public sealed partial class FileAgentSettingsStore : IAgentSettingsStore
{
    private readonly string _directory;

    private readonly SemaphoreSlim _saves = new(1, 1);

    /// <summary>
    /// Creates the store over a folder, which its first save creates when it is missing.
    /// </summary>
    /// <param name="directory">The folder, absolute or relative to the current directory.</param>
    public FileAgentSettingsStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
    }

    /// <inheritdoc />
    public async Task<StoredDocument?> LoadAsync(string kind, CancellationToken ct)
    {
        var path = PathOf(kind);
        return File.Exists(path) ? Read(await ReadAsync(path, ct)) : null;
    }

    /// <inheritdoc />
    public async Task<long> SaveAsync(string kind, string json, long loadedVersion, CancellationToken ct)
    {
        var path = PathOf(kind);
        await _saves.WaitAsync(ct);

        try
        {
            var stored = File.Exists(path) ? Read(await ReadAsync(path, ct)).Version : 0;

            if (stored != loadedVersion)
                throw new StoreConflictException($"The agent's {kind} was saved since version {loadedVersion}.");

            Directory.CreateDirectory(_directory);
            var written = $"{path}.saving";
            await File.WriteAllTextAsync(written, Write(json, loadedVersion + 1), ct);
            File.Move(written, path, overwrite: true);

            return loadedVersion + 1;
        }
        finally
        {
            _saves.Release();
        }
    }

    // A kind names a file of the folder, never a path out of it
    private string PathOf(string kind) =>
        FileName().IsMatch(kind) && kind is not ("." or "..")
            ? Path.Combine(_directory, $"{kind}.json")
            : throw new ArgumentException(
                $"The kind '{kind}' is no file name: give the agent a WithStorageKind of letters, digits, '_', '-' "
                + "and '.' only.",
                nameof(kind));

    // A load may read while a save moves the new file over this one, which Windows allows only to a reader that shares
    // deletion
    private static async Task<string> ReadAsync(string path, CancellationToken ct)
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

    // The settings as Typeshift wrote them, under the version a save must name
    private static StoredDocument Read(string file)
    {
        using var document = JsonDocument.Parse(file);
        var root = document.RootElement;

        return new StoredDocument(root.GetProperty("settings").GetRawText(), root.GetProperty("version").GetInt64());
    }

    private static string Write(string json, long version)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", version);
            writer.WritePropertyName("settings");
            writer.WriteRawValue(json);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex FileName();
}
