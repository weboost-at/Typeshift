using System.Reflection;
using System.Text.RegularExpressions;

namespace Typeshift.Tests.Testing.Conventions;

// Reads every test project's sources; tests are found in the source, so one copy covers all projects
public static partial class TestSources
{
    // Deterministic CI builds rewrite [CallerFilePath], the project file passes the real directory instead
    public static string TestsDirectory { get; } = typeof(TestSources).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "TestsDirectory")
        .Value!;

    // The folders under tests/ whose project file says it is a test project, so a new one is read unasked
    public static IReadOnlyList<string> Projects { get; } =
    [
        .. Directory.EnumerateDirectories(TestsDirectory)
            .Where(folder => Directory.EnumerateFiles(folder, "*.csproj").Any(IsTestProject))
            .Select(folder => Path.GetFileName(folder))
            .Order()
    ];

    // Every test file outside bin/, obj/ and the shared Testing/ folders, as "<Project>/<path in the project>"
    public static IReadOnlyList<string> Files { get; } =
    [
        .. Projects.SelectMany(project => Directory
            .EnumerateFiles(Path.Combine(TestsDirectory, project), "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Path.Combine(TestsDirectory, project), path).Replace('\\', '/'))
            .Where(path => !path.StartsWith("bin/") && !path.StartsWith("obj/") && !path.StartsWith("Testing/"))
            .Select(path => $"{project}/{path}"))
            .Order()
    ];

    public static IReadOnlyList<TestMethod> TestMethods { get; } = [.. Files.SelectMany(FindTestMethods)];

    public static string[] Lines(string file) => File.ReadAllLines(Path.Combine(TestsDirectory, file));

    public static string ProjectOf(string file) => file[..file.IndexOf('/')];

    // A declaration the conventions cannot read keeps its whole line as its name, which no naming rule accepts, so
    // a test never escapes them by its modifiers
    internal static string NameOf(string declaration) =>
        TestDeclaration().Match(declaration) is { Success: true } match
            ? match.Groups["name"].Value
            : declaration.Trim();

    private static bool IsTestProject(string project) =>
        File.ReadAllText(project).Contains("<IsTestProject>true</IsTestProject>");

    private static IEnumerable<TestMethod> FindTestMethods(string file)
    {
        var lines = Lines(file);
        var nameSpace = lines.Select(line => Namespace().Match(line)).FirstOrDefault(match => match.Success);
        var type = lines.Select(line => TestClass().Match(line)).FirstOrDefault(match => match.Success);
        if (nameSpace is null || type is null) yield break;

        for (var index = 0; index < lines.Length; index++)
        {
            if (!TestAttribute().IsMatch(lines[index])) continue;

            var declaration = Array.FindIndex(lines, index, line => Member().IsMatch(line));
            if (declaration < 0) yield break;

            yield return new TestMethod(
                File: file,
                Line: declaration + 1,
                Namespace: nameSpace.Groups["name"].Value,
                Class: type.Groups["name"].Value,
                Name: NameOf(lines[declaration]));
        }
    }

    [GeneratedRegex(@"^namespace (?<name>[\w.]+);")]
    private static partial Regex Namespace();

    [GeneratedRegex(@"^public (sealed |abstract |partial )*class (?<name>\w+)")]
    private static partial Regex TestClass();

    [GeneratedRegex(@"^\s*\[(Fact|Theory)\b")]
    internal static partial Regex TestAttribute();

    // The first line of a member, which follows a test's attributes
    [GeneratedRegex(@"^\s*(public|internal|protected|private|static|async)\b")]
    internal static partial Regex Member();

    [GeneratedRegex(@"^\s*public (async )?(Task|void|ValueTask) (?<name>\w+)\(")]
    internal static partial Regex TestDeclaration();
}
