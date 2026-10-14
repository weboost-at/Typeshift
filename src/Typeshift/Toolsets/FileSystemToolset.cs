using System.Text;
using System.Text.RegularExpressions;

namespace Typeshift.Toolsets;

/// <summary>
/// Reads and writes files within the root the host's <see cref="IFileSystemRoot"/> gives. Paths are relative to it;
/// absolute paths, <c>..</c> and links that lead outside are refused.
/// </summary>
public sealed partial class FileSystemToolset : Toolset
{
    // A search that runs longer than this on one line gives up on the pattern
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Declares the six file tools: the three that read are read-only, <c>delete_file</c> asks first.
    /// </summary>
    public FileSystemToolset()
    {
        WithDisplayName("Files");

        AddTool(ReadFileAsync)
            .ReadOnly()
            .WithSettings<FileSystemSettings>();

        AddTool(ListFiles)
            .ReadOnly();

        AddTool(SearchFilesAsync)
            .ReadOnly()
            .WithSettings<FileSystemSettings>();

        AddTool(WriteFileAsync);

        AddTool(EditFileAsync);

        AddTool(DeleteFile)
            .RequiresApproval();
    }

    /// <summary>
    /// Reads a text file, numbering its lines.
    /// </summary>
    /// <param name="path">The file, relative to the root.</param>
    /// <param name="settings">The limits.</param>
    /// <param name="services">The run's services.</param>
    /// <param name="offset">The first line to read, from 0.</param>
    /// <param name="limit">The most lines to read; capped by the settings.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The numbered lines, or why the file cannot be read.</returns>
    public static async Task<string> ReadFileAsync(
        string path,
        FileSystemSettings settings,
        IServiceProvider services,
        int offset = 0,
        int? limit = null,
        CancellationToken ct = default)
    {
        var root = FileSystemRoot.Of(services);
        if (FileSystemRoot.Resolve(root, path, out var refused) is not { } file) return refused!;
        if (!File.Exists(file)) return $"'{path}' is no file.";

        var count = Math.Min(limit ?? settings.MaxLines, settings.MaxLines);
        var read = new StringBuilder();
        var number = 0;

        await foreach (var line in File.ReadLinesAsync(file, ct))
        {
            if (number >= offset + count) return read.Append($"[More lines follow from line {number}.]").ToString();
            if (number >= offset) read.Append($"{number + 1,6}\t{line}\n");

            number++;
        }

        return read.Length > 0 ? read.ToString() : $"'{path}' has {number} lines.";
    }

    /// <summary>
    /// Lists the files and folders of a folder, folders ending in <c>/</c>.
    /// </summary>
    /// <param name="services">The run's services.</param>
    /// <param name="path">The folder, relative to the root.</param>
    /// <returns>The entries, one per line, or why the folder cannot be listed.</returns>
    public static string ListFiles(IServiceProvider services, string path = ".")
    {
        var root = FileSystemRoot.Of(services);
        if (FileSystemRoot.Resolve(root, path, out var refused) is not { } folder) return refused!;
        if (!Directory.Exists(folder)) return $"'{path}' is no folder.";

        var entries = new DirectoryInfo(folder).EnumerateFileSystemInfos()
            .Select(entry => entry is DirectoryInfo ? $"{entry.Name}/" : entry.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        return entries.Count == 0 ? $"'{path}' is empty." : string.Join('\n', entries);
    }

    /// <summary>
    /// Finds the lines that match a regular expression, in every file under a folder.
    /// </summary>
    /// <param name="pattern">A .NET regular expression.</param>
    /// <param name="settings">The limits.</param>
    /// <param name="services">The run's services.</param>
    /// <param name="path">The folder, relative to the root.</param>
    /// <param name="ct">Cancels the search.</param>
    /// <returns>The matches as path:line: text, or why the search failed.</returns>
    public static async Task<string> SearchFilesAsync(
        string pattern,
        FileSystemSettings settings,
        IServiceProvider services,
        string path = ".",
        CancellationToken ct = default)
    {
        var root = FileSystemRoot.Of(services);
        if (FileSystemRoot.Resolve(root, path, out var refused) is not { } folder) return refused!;
        if (!Directory.Exists(folder)) return $"'{path}' is no folder.";

        Regex regex;

        try
        {
            regex = new Regex(pattern, RegexOptions.None, PatternTimeout);
        }
        catch (ArgumentException exception)
        {
            return $"The pattern is not a .NET regular expression: {exception.Message}";
        }

        var matches = new List<string>();
        var files = Directory.EnumerateFiles(folder, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        });

        foreach (var file in files.Order(StringComparer.Ordinal))
        {
            var number = 0;

            await foreach (var line in File.ReadLinesAsync(file, ct))
            {
                number++;

                if (!Matches(regex, line)) continue;
                if (matches.Count == settings.MaxLines) return Joined(matches, "[More matches follow.]");

                matches.Add($"{FileSystemRoot.Relative(root, file)}:{number}: {line}");
            }
        }

        return matches.Count == 0 ? "No line matches." : Joined(matches, null);
    }

    /// <summary>
    /// Writes a text file, replacing it if it exists and creating its folders.
    /// </summary>
    /// <param name="path">The file, relative to the root.</param>
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
        var root = FileSystemRoot.Of(services);
        if (FileSystemRoot.Resolve(root, path, out var refused) is not { } file) return refused!;
        if (Directory.Exists(file)) return $"'{path}' is a folder.";

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, content, ct);

        return $"Wrote '{path}'.";
    }

    /// <summary>
    /// Replaces one exact occurrence of a text in a file.
    /// </summary>
    /// <param name="path">The file, relative to the root.</param>
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
        var root = FileSystemRoot.Of(services);
        if (FileSystemRoot.Resolve(root, path, out var refused) is not { } file) return refused!;
        if (!File.Exists(file)) return $"'{path}' is no file.";
        if (oldText.Length == 0) return "The text to replace is empty.";

        var content = await File.ReadAllTextAsync(file, ct);
        var first = content.IndexOf(oldText, StringComparison.Ordinal);

        if (first < 0) return $"'{path}' does not contain the text to replace.";

        if (content.IndexOf(oldText, first + oldText.Length, StringComparison.Ordinal) >= 0)
            return $"'{path}' contains the text to replace more than once; give more of it, so it occurs once.";

        var edited = string.Concat(content.AsSpan(0, first), newText, content.AsSpan(first + oldText.Length));

        await File.WriteAllTextAsync(file, edited, ct);
        return $"Edited '{path}'.";
    }

    /// <summary>
    /// Deletes a file.
    /// </summary>
    /// <param name="path">The file, relative to the root.</param>
    /// <param name="services">The run's services.</param>
    /// <returns>That the file was deleted, or why not.</returns>
    public static string DeleteFile(string path, IServiceProvider services)
    {
        var root = FileSystemRoot.Of(services);
        if (FileSystemRoot.Resolve(root, path, out var refused) is not { } file) return refused!;
        if (!File.Exists(file)) return $"'{path}' is no file.";

        File.Delete(file);
        return $"Deleted '{path}'.";
    }

    private static bool Matches(Regex regex, string line)
    {
        try
        {
            return regex.IsMatch(line);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static string Joined(List<string> lines, string? more) =>
        more is null ? string.Join('\n', lines) : $"{string.Join('\n', lines)}\n{more}";
}

/// <summary>
/// The limits of the file tools.
/// </summary>
public sealed record FileSystemSettings
{
    /// <summary>
    /// The most lines one read returns, and the most matches one search returns.
    /// </summary>
    public int MaxLines { get; init; } = 2000;
}
