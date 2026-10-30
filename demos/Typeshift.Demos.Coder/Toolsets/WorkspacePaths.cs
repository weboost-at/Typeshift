using Microsoft.Extensions.DependencyInjection;
using Typeshift.Demos.Coder.Hosting;

namespace Typeshift.Demos.Coder.Toolsets;

// A path the model sent, within the workspace: relative, without .., and through no link that leads outside. Paths
// compare after every link on them is followed, one hop at a time and the workspace's own included, as the operating
// system follows them. A link swapped in between the check and the use still escapes; only a sandbox stops that
internal static class WorkspacePaths
{
    // As many hops as Linux follows before it gives up on a path
    private const int MaxHops = 40;

    private static readonly StringComparison Comparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string Of(IServiceProvider services)
    {
        var workspace = services.GetRequiredService<WorkspaceOptions>().Path;

        var hops = 0;
        return Follow(Path.GetFullPath(workspace), ref hops)
            ?? throw new InvalidOperationException($"The workspace '{workspace}' leads through too many links.");
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
