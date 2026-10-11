using System.Reflection;
using System.Text.RegularExpressions;

namespace Agentic.Tests.Testing.Conventions;

// Reads every test project's sources; tests are found in the source, so one copy covers all projects
public static partial class TestSources
{
    private static readonly string[] Projects = ["Agentic.Tests", "Agentic.Generators.Tests"];

    // Deterministic CI builds rewrite [CallerFilePath], the project file passes the real directory instead
    public static string TestsDirectory { get; } = typeof(TestSources).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "TestsDirectory")
        .Value!;

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

    private static IEnumerable<TestMethod> FindTestMethods(string file)
    {
        var lines = Lines(file);
        var nameSpace = lines.Select(line => Namespace().Match(line)).FirstOrDefault(match => match.Success);
        var type = lines.Select(line => TestClass().Match(line)).FirstOrDefault(match => match.Success);
        if (nameSpace is null || type is null) yield break;

        for (var index = 0; index < lines.Length; index++)
        {
            if (!TestAttribute().IsMatch(lines[index])) continue;

            var declaration = Array.FindIndex(lines, index, line => TestDeclaration().IsMatch(line));
            if (declaration < 0) yield break;

            yield return new TestMethod(
                File: file,
                Namespace: nameSpace.Groups["name"].Value,
                Class: type.Groups["name"].Value,
                Name: TestDeclaration().Match(lines[declaration]).Groups["name"].Value);
        }
    }

    [GeneratedRegex(@"^namespace (?<name>[\w.]+);")]
    private static partial Regex Namespace();

    [GeneratedRegex(@"^public (sealed |abstract |partial )*class (?<name>\w+)")]
    private static partial Regex TestClass();

    [GeneratedRegex(@"^\s*\[(Fact|Theory)\b")]
    internal static partial Regex TestAttribute();

    [GeneratedRegex(@"^\s*public (async )?(Task|void|ValueTask) (?<name>\w+)\(")]
    internal static partial Regex TestDeclaration();
}
