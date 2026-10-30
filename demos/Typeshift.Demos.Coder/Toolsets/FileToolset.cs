using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Demos.Coder.Hosting;

namespace Typeshift.Demos.Coder.Toolsets;

// The file tools of the workspace: the three that read are read-only, so plan mode and the explorer may use them
internal partial class FileToolset : Toolset
{
    public FileToolset()
    {
        AddTool(ReadFileAsync)
            .ReadOnly()
            .WithSettings<ReadFileSettings>();

        AddTool(ListFiles)
            .ReadOnly();

        AddTool(SearchFilesAsync)
            .ReadOnly();

        AddTool(WriteFileAsync);

        AddTool(EditFileAsync);
    }

    /// <summary>
    /// Reads a text file, numbering its lines.
    /// </summary>
    /// <param name="path">The file, relative to the workspace.</param>
    /// <param name="settings">The limits.</param>
    /// <param name="services">The run's services.</param>
    /// <param name="offset">The first line to read, from 0.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The numbered lines, or why the file cannot be read.</returns>
    public static async Task<string> ReadFileAsync(
        string path,
        ReadFileSettings settings,
        IServiceProvider services,
        int offset = 0,
        CancellationToken ct = default)
    {
        if (Workspace(services).Resolve(path, out var refused) is not { } file) return refused!;
        if (!File.Exists(file)) return $"'{path}' is no file.";

        var lines = await File.ReadAllLinesAsync(file, ct);
        var read = new StringBuilder();
        var from = Math.Max(0, offset);

        foreach (var number in Enumerable.Range(from, Math.Clamp(lines.Length - from, 0, settings.MaxLines)))
        {
            read.Append($"{number + 1,6}\t{lines[number]}\n");
        }

        if (from + settings.MaxLines < lines.Length)
            read.Append($"[More lines follow from line {from + settings.MaxLines}.]");

        return read.Length > 0 ? read.ToString() : $"'{path}' has {lines.Length} lines.";
    }

    /// <summary>
    /// Lists the files and folders of a folder, folders ending in <c>/</c>.
    /// </summary>
    /// <param name="services">The run's services.</param>
    /// <param name="path">The folder, relative to the workspace.</param>
    /// <returns>The entries, one per line, or why the folder cannot be listed.</returns>
    public static string ListFiles(IServiceProvider services, string path = ".")
    {
        if (Workspace(services).Resolve(path, out var refused) is not { } folder) return refused!;
        if (!Directory.Exists(folder)) return $"'{path}' is no folder.";

        var entries = new DirectoryInfo(folder).EnumerateFileSystemInfos()
            .Select(entry => entry is DirectoryInfo ? $"{entry.Name}/" : entry.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        return entries.Count > 0 ? string.Join('\n', entries) : $"'{path}' is empty.";
    }

    /// <summary>
    /// Finds the lines that contain a text, in every file under a folder.
    /// </summary>
    /// <param name="pattern">The text to find, matched case by case.</param>
    /// <param name="services">The run's services.</param>
    /// <param name="path">The folder, relative to the workspace.</param>
    /// <param name="ct">Cancels the search.</param>
    /// <returns>The matches as path:line: text, or why the search failed.</returns>
    public static async Task<string> SearchFilesAsync(
        string pattern,
        IServiceProvider services,
        string path = ".",
        CancellationToken ct = default)
    {
        var workspace = Workspace(services);
        if (workspace.Resolve(path, out var refused) is not { } folder) return refused!;
        if (!Directory.Exists(folder)) return $"'{path}' is no folder.";

        var matches = new List<string>();

        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Order())
        {
            var lines = await File.ReadAllLinesAsync(file, ct);

            matches.AddRange(lines
                .Select((line, index) => (line, index))
                .Where(match => match.line.Contains(pattern, StringComparison.Ordinal))
                .Select(match => $"{workspace.Relative(file)}:{match.index + 1}: {match.line.Trim()}"));
        }

        return matches.Count > 0 ? string.Join('\n', matches.Take(200)) : $"No line under '{path}' holds '{pattern}'.";
    }

    /// <summary>
    /// Writes a whole file, creating it and its folders, or replacing what it held.
    /// </summary>
    /// <param name="path">The file, relative to the workspace.</param>
    /// <param name="content">The whole content.</param>
    /// <param name="services">The run's services.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>What was written, or why not.</returns>
    public static async Task<string> WriteFileAsync(
        string path,
        string content,
        IServiceProvider services,
        CancellationToken ct)
    {
        if (Workspace(services).Resolve(path, out var refused) is not { } file) return refused!;

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, content, ct);

        return $"Wrote {content.Length} characters to '{path}'.";
    }

    /// <summary>
    /// Replaces one text in a file with another. Read the file first; the old text must occur exactly once.
    /// </summary>
    /// <param name="path">The file, relative to the workspace.</param>
    /// <param name="oldText">The text to replace; it must occur exactly once.</param>
    /// <param name="newText">The replacement.</param>
    /// <param name="services">The run's services.</param>
    /// <param name="ct">Cancels the edit.</param>
    /// <returns>What changed, or why nothing did.</returns>
    public static async Task<string> EditFileAsync(
        string path,
        string oldText,
        string newText,
        IServiceProvider services,
        CancellationToken ct)
    {
        if (Workspace(services).Resolve(path, out var refused) is not { } file) return refused!;
        if (!File.Exists(file)) return $"'{path}' is no file.";

        var text = await File.ReadAllTextAsync(file, ct);
        var at = text.IndexOf(oldText, StringComparison.Ordinal);

        if (at < 0) return $"'{path}' does not contain the old text.";
        if (text.IndexOf(oldText, at + 1, StringComparison.Ordinal) >= 0)
            return $"'{path}' contains the old text more than once; give more of it.";

        var edited = string.Concat(text.AsSpan(0, at), newText, text.AsSpan(at + oldText.Length));
        await File.WriteAllTextAsync(file, edited, ct);
        return $"Edited '{path}'.";
    }

    private static WorkspaceOptions Workspace(IServiceProvider services) =>
        services.GetRequiredService<WorkspaceOptions>();
}

/// <summary>
/// The limits of read_file.
/// </summary>
internal sealed record ReadFileSettings
{
    /// <summary>
    /// The most lines one read returns.
    /// </summary>
    public int MaxLines { get; init; } = 2000;
}
