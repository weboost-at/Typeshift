using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Agentic.Generators.Tests.Testing;

// Snapshots/{TestClass}.{TestName}.verified.txt; a mismatch writes .received.txt, ACCEPT_SNAPSHOTS=1 accepts it
public static class Snapshot
{
    private const string AcceptVariable = "ACCEPT_SNAPSHOTS";

    private static readonly string Directory = typeof(Snapshot).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "SnapshotsDirectory")
        .Value!;

    public static void Verify(
        GeneratorDriverRunResult result,
        [CallerMemberName] string testName = "",
        [CallerFilePath] string testFile = "")
    {
        var actual = Render(result);
        var name = $"{Path.GetFileNameWithoutExtension(testFile)}.{testName}";
        var verifiedPath = Path.Combine(Directory, $"{name}.verified.txt");
        var receivedPath = Path.Combine(Directory, $"{name}.received.txt");

        System.IO.Directory.CreateDirectory(Directory);

        if (Environment.GetEnvironmentVariable(AcceptVariable) == "1")
        {
            File.WriteAllText(verifiedPath, actual);
            File.Delete(receivedPath);
            return;
        }

        var expected = File.Exists(verifiedPath) ? Normalize(File.ReadAllText(verifiedPath)) : null;

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

        return Normalize(builder.ToString());
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";
}
