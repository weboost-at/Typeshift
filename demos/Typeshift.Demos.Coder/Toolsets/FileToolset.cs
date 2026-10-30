using System.Text;
using System.Text.RegularExpressions;

namespace Typeshift.Demos.Coder.Toolsets;

// Reads and writes files within the workspace, ported from the built-in toolset 0.1.0-alpha.4 shipped. Paths are
// relative to it; absolute paths, .. and links that lead outside are refused
internal partial class FileToolset : Toolset
{
    // shortcut: a file edited whole in memory; larger ones are refused, line-wise tools would lift the limit
    private const long MaxEditSize = 16 * 1024 * 1024;

    // A pattern linear matching cannot run gets this long per line, and the search stops at its first timeout
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(1);

    // The three that read are read-only, so plan mode and the explorer may use them; delete_file asks first
    public FileToolset()
    {
        AddTool(ReadFileAsync)
            .ReadOnly()
            .WithSettings<FileSettings>();

        AddTool(ListFiles)
            .ReadOnly()
            .WithSettings<FileSettings>();

        AddTool(SearchFilesAsync)
            .ReadOnly()
            .WithSettings<FileSettings>();

        AddTool(WriteFileAsync);

        AddTool(EditFileAsync);

        AddTool(DeleteFile)
            .RequiresApproval();
    }

    /// <summary>
    /// Reads a text file, numbering its lines.
    /// </summary>
    /// <param name="path">The file, relative to the workspace.</param>
    /// <param name="settings">The limits.</param>
    /// <param name="services">The run's services.</param>
    /// <param name="offset">The first line to read, from 0.</param>
    /// <param name="limit">The most lines to read; capped by the settings.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The numbered lines, or why the file cannot be read.</returns>
    public static async Task<string> ReadFileAsync(
        string path,
        FileSettings settings,
        IServiceProvider services,
        int offset = 0,
        int? limit = null,
        CancellationToken ct = default)
    {
        var root = WorkspacePaths.Of(services);
        if (WorkspacePaths.Resolve(root, path, out var refused) is not { } file) return refused!;
        if (!File.Exists(file)) return $"'{path}' is no file.";

        var count = Math.Min(limit ?? settings.MaxLines, settings.MaxLines);
        var read = new StringBuilder();
        var number = 0;

        await foreach (var line in Lines(file, settings, ct))
        {
            if (number >= offset + count || read.Length >= settings.MaxOutputLength)
                return read.Append($"[More lines follow from line {number}.]").ToString();

            if (number >= offset) read.Append($"{number + 1,6}\t{line}\n");

            number++;
        }

        return read.Length > 0 ? read.ToString() : $"'{path}' has {number} lines.";
    }

    /// <summary>
    /// Lists the files and folders of a folder, folders ending in <c>/</c>.
    /// </summary>
    /// <param name="settings">The limits.</param>
    /// <param name="services">The run's services.</param>
    /// <param name="path">The folder, relative to the workspace.</param>
    /// <returns>The entries, one per line, or why the folder cannot be listed.</returns>
    public static string ListFiles(FileSettings settings, IServiceProvider services, string path = ".")
    {
        var root = WorkspacePaths.Of(services);
        if (WorkspacePaths.Resolve(root, path, out var refused) is not { } folder) return refused!;
        if (!Directory.Exists(folder)) return $"'{path}' is no folder.";

        var entries = new DirectoryInfo(folder).EnumerateFileSystemInfos()
            .Select(entry => entry is DirectoryInfo ? $"{entry.Name}/" : entry.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (entries.Count == 0) return $"'{path}' is empty.";

        return entries.Count > settings.MaxLines
            ? Joined([.. entries.Take(settings.MaxLines)], $"[{entries.Count - settings.MaxLines} more entries.]")
            : Joined(entries, null);
    }

    /// <summary>
    /// Finds the lines that match a regular expression, in every file under a folder.
    /// </summary>
    /// <param name="pattern">A .NET regular expression.</param>
    /// <param name="settings">The limits.</param>
    /// <param name="services">The run's services.</param>
    /// <param name="path">The folder, relative to the workspace.</param>
    /// <param name="ct">Cancels the search.</param>
    /// <returns>The matches as path:line: text, or why the search failed.</returns>
    public static async Task<string> SearchFilesAsync(
        string pattern,
        FileSettings settings,
        IServiceProvider services,
        string path = ".",
        CancellationToken ct = default)
    {
        var root = WorkspacePaths.Of(services);
        if (WorkspacePaths.Resolve(root, path, out var refused) is not { } folder) return refused!;
        if (!Directory.Exists(folder)) return $"'{path}' is no folder.";

        if (Compile(pattern, out var invalid) is not { } regex) return invalid!;

        var matches = new List<string>();
        var length = 0;
        var files = Directory.EnumerateFiles(folder, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        });

        foreach (var file in files.Order(StringComparer.Ordinal))
        {
            var number = 0;

            await foreach (var line in Lines(file, settings, ct))
            {
                number++;

                if (!Matches(regex, line, out var slow)) continue;
                if (slow) return "The pattern is too slow on these files; simplify it.";

                if (matches.Count == settings.MaxLines || length >= settings.MaxOutputLength)
                    return Joined(matches, "[More matches follow.]");

                var match = $"{WorkspacePaths.Relative(root, file)}:{number}: {line}";
                matches.Add(match);
                length += match.Length;
            }
        }

        return matches.Count == 0 ? "No line matches." : Joined(matches, null);
    }

    /// <summary>
    /// Writes a text file, replacing it if it exists and creating its folders.
    /// </summary>
    /// <param name="path">The file, relative to the workspace.</param>
    /// <param name="content">The whole content.</param>
    /// <param name="services">The run's services.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>That the file was written, or why not.</returns>
    public static async Task<string> WriteFileAsync(
        string path,
        string content,
        IServiceProvider services,
        CancellationToken ct)
    {
        var root = WorkspacePaths.Of(services);
        if (WorkspacePaths.Resolve(root, path, out var refused) is not { } file) return refused!;
        if (Directory.Exists(file)) return $"'{path}' is a folder.";

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        // A write cut short would leave the file half written
        await File.WriteAllTextAsync(file, content, CancellationToken.None);

        return $"Wrote '{path}'.";
    }

    /// <summary>
    /// Replaces one exact occurrence of a text in a file.
    /// </summary>
    /// <param name="path">The file, relative to the workspace.</param>
    /// <param name="oldText">The text to replace; it must occur exactly once.</param>
    /// <param name="newText">The replacement.</param>
    /// <param name="services">The run's services.</param>
    /// <param name="ct">Cancels the edit.</param>
    /// <returns>That the file was edited, or why not.</returns>
    public static async Task<string> EditFileAsync(
        string path,
        string oldText,
        string newText,
        IServiceProvider services,
        CancellationToken ct)
    {
        var root = WorkspacePaths.Of(services);
        if (WorkspacePaths.Resolve(root, path, out var refused) is not { } file) return refused!;
        if (!File.Exists(file)) return $"'{path}' is no file.";
        if (oldText.Length == 0) return "The text to replace is empty.";
        if (new FileInfo(file).Length > MaxEditSize) return $"'{path}' is too large to edit; write it whole instead.";

        var content = await File.ReadAllTextAsync(file, ct);
        var first = content.IndexOf(oldText, StringComparison.Ordinal);

        if (first < 0) return $"'{path}' does not contain the text to replace.";

        if (content.IndexOf(oldText, first + oldText.Length, StringComparison.Ordinal) >= 0)
            return $"'{path}' contains the text to replace more than once; give more of it, so it occurs once.";

        var edited = string.Concat(content.AsSpan(0, first), newText, content.AsSpan(first + oldText.Length));

        await File.WriteAllTextAsync(file, edited, CancellationToken.None);
        return $"Edited '{path}'.";
    }

    /// <summary>
    /// Deletes a file.
    /// </summary>
    /// <param name="path">The file, relative to the workspace.</param>
    /// <param name="services">The run's services.</param>
    /// <returns>That the file was deleted, or why not.</returns>
    public static string DeleteFile(string path, IServiceProvider services)
    {
        var root = WorkspacePaths.Of(services);
        if (WorkspacePaths.Resolve(root, path, out var refused) is not { } file) return refused!;
        if (!File.Exists(file)) return $"'{path}' is no file.";

        File.Delete(file);
        return $"Deleted '{path}'.";
    }

    // Linear matching where the pattern allows it, so no pattern stalls a search
    private static Regex? Compile(string pattern, out string? invalid)
    {
        invalid = null;

        try
        {
            return new Regex(pattern, RegexOptions.NonBacktracking);
        }
        catch (NotSupportedException)
        {
            return new Regex(pattern, RegexOptions.None, PatternTimeout);
        }
        catch (ArgumentException exception)
        {
            invalid = $"The pattern is not a .NET regular expression: {exception.Message}";
            return null;
        }
    }

    private static bool Matches(Regex regex, string line, out bool slow)
    {
        slow = false;

        try
        {
            return regex.IsMatch(line);
        }
        catch (RegexMatchTimeoutException)
        {
            slow = true;
            return true;
        }
    }

    // A file's lines, each cut at the settings' length, without holding a longer line in memory
    private static async IAsyncEnumerable<string> Lines(
        string file,
        FileSettings settings,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(file);
        var line = new StringBuilder();
        var buffer = new char[4096];
        var cut = false;
        int read;

        while ((read = await reader.ReadAsync(buffer, ct)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var character = buffer[i];

                if (character == '\n')
                {
                    yield return Ended(line, cut);
                    line.Clear();
                    cut = false;
                }
                else if (line.Length < settings.MaxLineLength)
                {
                    line.Append(character);
                }
                else
                {
                    cut = true;
                }
            }
        }

        if (line.Length > 0 || cut) yield return Ended(line, cut);
    }

    private static string Ended(StringBuilder line, bool cut)
    {
        var text = line.ToString().TrimEnd('\r');
        return cut ? $"{text}[cut]" : text;
    }

    private static string Joined(List<string> lines, string? more) =>
        more is null ? string.Join('\n', lines) : $"{string.Join('\n', lines)}\n{more}";
}

/// <summary>
/// The limits of the file tools.
/// </summary>
internal sealed record FileSettings
{
    /// <summary>
    /// The most lines one read returns, the most matches one search returns, and the most entries one list returns.
    /// </summary>
    public int MaxLines { get; init; } = 2000;

    /// <summary>
    /// The most characters of one line the model receives; a longer line is cut.
    /// </summary>
    public int MaxLineLength { get; init; } = 2000;

    /// <summary>
    /// The most characters one read or search returns.
    /// </summary>
    public int MaxOutputLength { get; init; } = 64 * 1024;
}
