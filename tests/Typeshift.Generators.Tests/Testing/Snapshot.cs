using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace Typeshift.Generators.Tests.Testing;

// Snapshots/{TestClass}.{TestName}.verified.txt; a mismatch writes .received.txt, ACCEPT_SNAPSHOTS=1 accepts it
public static partial class Snapshot
{
    private const string AcceptVariable = "ACCEPT_SNAPSHOTS";

    private static readonly string Directory = typeof(Snapshot).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "SnapshotsDirectory")
        .Value!;

    public static void Verify(
        GeneratorDriverRunResult result,
        [CallerMemberName] string testName = "",
        [CallerFilePath] string testFile = "") =>
        Verify($"{Path.GetFileNameWithoutExtension(testFile)}.{testName}", Render(result));

    // Snapshots/{name}.verified.txt, for text that belongs to the repository rather than to one test's source
    public static void Verify(string name, string text)
    {
        var actual = Normalize(text);
        var verifiedPath = Path.Combine(Directory, $"{name}.verified.txt");
        var receivedPath = Path.Combine(Directory, $"{name}.received.txt");

        System.IO.Directory.CreateDirectory(Directory);

        if (Environment.GetEnvironmentVariable(AcceptVariable) == "1")
        {
            File.WriteAllText(verifiedPath, actual);
            File.Delete(receivedPath);
            return;
        }

        var expected = Read(name);

        if (expected == actual)
        {
            File.Delete(receivedPath);
            return;
        }

        File.WriteAllText(receivedPath, actual);
        Assert.Fail(
            $"{(expected is null ? "No verified snapshot" : "Snapshot mismatch")} for {name}. Review {receivedPath} "
            + $"and rename it to .verified.txt, or rerun with {AcceptVariable}=1.");
    }

    // The verified text under a name, or null before its first snapshot
    public static string? Read(string name) =>
        Path.Combine(Directory, $"{name}.verified.txt") is var path && File.Exists(path)
            ? Normalize(File.ReadAllText(path))
            : null;

    private static string Render(GeneratorDriverRunResult result)
    {
        var builder = new StringBuilder();

        var sources = result.Results
            .SelectMany(generator => generator.GeneratedSources)
            .OrderBy(source => source.HintName, StringComparer.Ordinal);

        foreach (var source in sources)
        {
            builder.Append("// HintName: ").AppendLine(source.HintName);
            builder.AppendLine(source.SourceText.ToString().TrimEnd());
            builder.AppendLine();
        }

        var diagnostics = result.Diagnostics
            .OrderBy(diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .ToList();

        if (diagnostics.Count > 0) builder.AppendLine("// Diagnostics:");

        foreach (var diagnostic in diagnostics)
        {
            builder.Append("// ").AppendLine(diagnostic.ToString());
        }

        return Normalize(InterceptedLocation().Replace(builder.ToString(), "<location>"));
    }

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";

    // The data hashes the text of the intercepted call's file, so it differs with every edit and every line ending
    [GeneratedRegex(@"(?<=InterceptsLocationAttribute\(\d+, "")[^""]+")]
    private static partial Regex InterceptedLocation();
}
