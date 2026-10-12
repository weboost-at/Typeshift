using System.Reflection;

namespace Typeshift.Generators.Tests.Repository.Support;

// The repository's own files, for the tests that keep its docs and code in step
public static class RepositoryFiles
{
    // Deterministic CI builds rewrite [CallerFilePath], the project file passes the real directory instead
    public static string Root { get; } = typeof(RepositoryFiles).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "RepositoryDirectory")
        .Value!;

    public static string Read(string path) => File.ReadAllText(Path.Combine(Root, path));

    // Every file under the folders, outside bin/ and obj/, as a path from the repository root
    public static IReadOnlyList<string> Find(string pattern, params string[] folders) =>
    [
        .. folders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(Root, folder), pattern, SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(Root, path).Replace('\\', '/'))
            .Where(path => !path.Contains("/bin/") && !path.Contains("/obj/"))
            .Order()
    ];
}
