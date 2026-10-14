namespace Typeshift.Toolsets;

/// <summary>
/// The folder the file system and shell tools stay within; resolved from the run's services, so it may differ per
/// customer. Registered with <c>WithFileSystemRoot&lt;T&gt;()</c>.
/// </summary>
public interface IFileSystemRoot
{
    /// <summary>
    /// The folder's absolute path.
    /// </summary>
    string Path { get; }
}

// D37: a path the model sent, within the root: relative, without .., and through no link that leads outside. Paths
// compare after every link on them is followed, the root's own included
internal static class FileSystemRoot
{
    private static readonly StringComparison Comparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string Of(IServiceProvider services)
    {
        var root = services.GetService(typeof(IFileSystemRoot)) as IFileSystemRoot
            ?? throw new InvalidOperationException(
                "The file system and shell tools need a root: call WithFileSystemRoot<T>() after AddTypeshift().");

        var full = Path.GetFullPath(root.Path);
        return Follow(Path.GetPathRoot(full)!, Path.GetRelativePath(Path.GetPathRoot(full)!, full).Split(Separators));
    }

    // Null, with the reason, for a path the tools refuse
    public static string? Resolve(string root, string path, out string? refused)
    {
        refused = null;
        var segments = path.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

        if (Path.IsPathRooted(path))
            refused = $"'{path}' is absolute; give a path relative to the workspace.";
        else if (segments.Contains(".."))
            refused = $"'{path}' leads up with '..'; give a path within the workspace.";
        else if (Follow(root, segments) is var resolved && Within(root, resolved))
            return resolved;
        else
            refused = $"'{path}' leads outside the workspace.";

        return null;
    }

    public static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

    private static char[] Separators => ['/', '\\'];

    // A link is followed to where it finally leads, and the rest of the path goes on from there
    private static string Follow(string from, IEnumerable<string> segments)
    {
        var current = from;

        foreach (var segment in segments.Where(segment => segment is not ("." or "")))
        {
            current = Path.Combine(current, segment);
            if (Info(current)?.ResolveLinkTarget(returnFinalTarget: true) is { } target) current = target.FullName;
        }

        return current;
    }

    private static bool Within(string root, string path) =>
        string.Equals(path, root, Comparison)
        || path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, Comparison);

    private static FileSystemInfo? Info(string path) =>
        Directory.Exists(path) ? new DirectoryInfo(path) : File.Exists(path) ? new FileInfo(path) : null;
}
