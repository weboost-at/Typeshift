namespace Typeshift.Toolsets;

/// <summary>
/// The folder the file system and shell tools stay within; resolved from the run's services, so it may differ per
/// customer. Registered with <c>WithFileSystemRoot&lt;T&gt;()</c>, or <c>WithFileSystemRoot(path)</c> for one folder.
/// </summary>
public interface IFileSystemRoot
{
    /// <summary>
    /// The folder's absolute path.
    /// </summary>
    string Path { get; }
}

// The root WithFileSystemRoot(path) registers, the same folder for every run
internal sealed record FolderRoot(string Path) : IFileSystemRoot;

// D37: a path the model sent, within the root: relative, without .., and through no link that leads outside. Paths
// compare after every link on them is followed, one hop at a time and the root's own included, as the operating
// system follows them. shortcut: a link swapped in between the check and the use still escapes; only a sandbox
// stops that
internal static class FileSystemRoot
{
    // As many hops as Linux follows before it gives up on a path
    private const int MaxHops = 40;

    private static readonly StringComparison Comparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string Of(IServiceProvider services)
    {
        var root = services.GetService(typeof(IFileSystemRoot)) as IFileSystemRoot
            ?? throw new InvalidOperationException(
                "The file system and shell tools need a root: call WithFileSystemRoot(path) or WithFileSystemRoot<T>() "
                + "after AddTypeshift().");

        var hops = 0;
        return Follow(Path.GetFullPath(root.Path), ref hops)
            ?? throw new InvalidOperationException($"The root '{root.Path}' leads through too many links.");
    }

    // Null, with the reason, for a path the tools refuse
    public static string? Resolve(string root, string path, out string? refused)
    {
        refused = null;
        var segments = path.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        var hops = 0;

        if (Path.IsPathRooted(path))
            refused = $"'{path}' is absolute; give a path relative to the workspace.";
        else if (segments.Contains(".."))
            refused = $"'{path}' leads up with '..'; give a path within the workspace.";
        // Compared as the operating system opens it: Windows drops the trailing dots and spaces of a last segment
        else if (Follow(root, segments, ref hops) is { } followed
                 && Path.GetFullPath(followed) is var resolved
                 && Within(root, resolved))
            return resolved;
        else
            refused = $"'{path}' leads outside the workspace.";

        return null;
    }

    public static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

    private static char[] Separators => ['/', '\\'];

    // An absolute path, every link on it followed; null past the hops a path may take
    private static string? Follow(string path, ref int hops)
    {
        var start = Path.GetPathRoot(path)!;
        return Follow(start, path[start.Length..].Split(Separators), ref hops);
    }

    // Each part in turn: a link is replaced by its target, read relative to the link's folder, whose own parts are
    // followed in turn; .. in a target leaves the folder the path has reached, as the operating system's does
    private static string? Follow(string from, IEnumerable<string> segments, ref int hops)
    {
        var current = from;

        foreach (var segment in segments.Where(segment => segment is not ("." or "")))
        {
            if (segment == "..")
            {
                current = Path.GetDirectoryName(current) ?? current;
                continue;
            }

            current = Path.Combine(current, segment);

            if (new FileInfo(current).LinkTarget is not { } target) continue;
            if (++hops > MaxHops) return null;

            var next = Path.IsPathRooted(target)
                ? Follow(target, ref hops)
                : Follow(Path.GetDirectoryName(current)!, target.Split(Separators), ref hops);

            if (next is null) return null;
            current = next;
        }

        return current;
    }

    private static bool Within(string root, string path) =>
        string.Equals(path, root, Comparison)
        || path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, Comparison);

}
