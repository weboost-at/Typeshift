using Microsoft.CodeAnalysis;

namespace Typeshift.Generators.Tests.Testing;

public sealed record GeneratorRun(GeneratorDriver Driver, Compilation Output)
{
    public GeneratorDriverRunResult Result => Driver.GetRunResult();

    public IReadOnlyList<Diagnostic> Diagnostics => Result.Diagnostics;

    public IReadOnlyList<Diagnostic> CompilationDiagnostics => Output.GetDiagnostics();

    public IEnumerable<string> HintNames => Result.GeneratedTrees.Select(tree => Path.GetFileName(tree.FilePath));
}
