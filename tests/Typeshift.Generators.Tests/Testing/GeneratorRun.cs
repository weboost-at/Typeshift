using Microsoft.CodeAnalysis;

namespace Typeshift.Generators.Tests.Testing;

// Diagnostics are the compiler's and the analyzers', over the subject and the generated code, suppressed ones
// marked as such
public sealed record GeneratorRun(GeneratorDriver Driver, Compilation Output, IReadOnlyList<Diagnostic> Diagnostics)
{
    public GeneratorDriverRunResult Result => Driver.GetRunResult();

    public IEnumerable<Diagnostic> Reported => Diagnostics.Where(diagnostic => !diagnostic.IsSuppressed);

    public IEnumerable<string> HintNames => Result.GeneratedTrees.Select(tree => Path.GetFileName(tree.FilePath));

    public string Source(string hintName) =>
        Result.GeneratedTrees.Single(tree => Path.GetFileName(tree.FilePath) == hintName).ToString();
}
