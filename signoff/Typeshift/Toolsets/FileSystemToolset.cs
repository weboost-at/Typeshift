namespace Typeshift.Toolsets;

/// <summary>
/// Reads and writes files within the root the host's <see cref="IFileSystemRoot"/> gives. Paths are relative to it;
/// absolute paths, <c>..</c> and links that lead outside are refused.
/// </summary>
public sealed partial class FileSystemToolset : Toolset
{
    public FileSystemToolset()
    {
        WithDisplayName("Files");

        AddTool(ReadFileAsync)
            .WithSettings<FileSystemSettings>();

        AddTool(ListFiles);

        AddTool(SearchFilesAsync)
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
    /// <param name="offset">The first line to read, from 0.</param>
    /// <param name="limit">The most lines to read; capped by the settings.</param>
    public static Task<string> ReadFileAsync(
        string path,
        FileSystemSettings settings,
        IServiceProvider services,
        int offset = 0,
        int? limit = null,
        CancellationToken ct = default) =>
        throw null!;

    /// <summary>
    /// Lists the files and folders of a folder.
    /// </summary>
    /// <param name="path">The folder, relative to the root.</param>
    public static IReadOnlyList<string> ListFiles(IServiceProvider services, string path = ".") => throw null!;

    /// <summary>
    /// Finds the lines that match a regular expression, in every file under a folder.
    /// </summary>
    /// <param name="pattern">A .NET regular expression.</param>
    /// <param name="path">The folder, relative to the root.</param>
    public static Task<IReadOnlyList<string>> SearchFilesAsync(
        string pattern,
        FileSystemSettings settings,
        IServiceProvider services,
        string path = ".",
        CancellationToken ct = default) =>
        throw null!;

    /// <summary>
    /// Writes a text file, replacing it if it exists and creating its folders.
    /// </summary>
    /// <param name="path">The file, relative to the root.</param>
    /// <param name="content">The whole content.</param>
    public static Task WriteFileAsync(string path, string content, IServiceProvider services, CancellationToken ct) =>
        throw null!;

    /// <summary>
    /// Replaces one exact occurrence of a text in a file.
    /// </summary>
    /// <param name="path">The file, relative to the root.</param>
    /// <param name="oldText">The text to replace; it must occur exactly once.</param>
    /// <param name="newText">The replacement.</param>
    public static Task EditFileAsync(
        string path,
        string oldText,
        string newText,
        IServiceProvider services,
        CancellationToken ct) =>
        throw null!;

    /// <summary>
    /// Deletes a file.
    /// </summary>
    /// <param name="path">The file, relative to the root.</param>
    public static void DeleteFile(string path, IServiceProvider services) => throw null!;
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

/// <summary>
/// The folder the file system and shell tools stay within; resolved from the run's services, so it may differ per
/// customer.
/// </summary>
public interface IFileSystemRoot
{
    /// <summary>
    /// The folder's absolute path.
    /// </summary>
    string Path { get; }
}
