using System.Globalization;
using Typeshift.Generators.Tests.Incremental.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Typeshift.Tools.Generators;

// What one generator run costs after an edit of an unrelated file. Every output stays cached, yet the transforms
// bind the constructors of each toolset and agent again (D3), so the cost grows with their number
internal static class GeneratorCost
{
    private const int Edits = 15;

    private static readonly int[] Sizes = [0, 5, 10, 50, 100];

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static void Print()
    {
        // The first runs pay for the JIT, which no later one does
        Rerun(ScaleSources.Pair, pairs: 20);
        Rerun(TenToolSources.Pair, pairs: 20);

        Console.WriteLine("A generator run after an edit of an unrelated file: the least CPU time of 15 runs and");
        Console.WriteLine("their median allocation. Every output has to stay cached.");

        Print("A toolset of three tools, one with settings, and an agent of two toolsets", ScaleSources.Pair);
        Print("A toolset of ten tools that share one settings type, and an agent", TenToolSources.Pair);
        PrintStages(pairs: 50);
    }

    private static void Print(string shape, Func<int, int, string> pair)
    {
        Console.WriteLine();
        Console.WriteLine(shape);
        Console.WriteLine("  pairs    CPU ms    allocated B    B per pair  outputs");

        foreach (var pairs in Sizes)
        {
            var (cpu, bytes, cached) = Rerun(pair, pairs);
            var each = pairs == 0 ? "-" : (bytes / pairs).ToString("N0", Invariant);
            var outputs = cached ? "cached" : "WRITTEN AGAIN";

            Console.WriteLine(string.Create(Invariant, $"  {pairs,5} {cpu,9:F1} {bytes,14:N0} {each,13}  {outputs}"));
        }
    }

    // The two steps a transform takes before the parsers, through the compiler's own API, on a compilation that
    // has bound nothing yet, as the one an edit makes
    private static void PrintStages(int pairs)
    {
        var compilation = Compile(ScaleSources.Pair, pairs);
        (long Symbols, long Operations) bound = default;

        for (var pass = 0; pass < 3; pass++)
        {
            bound = Bind(Edited(compilation, pairs, pass));
        }

        var (_, bytes, _) = Rerun(ScaleSources.Pair, pairs);
        var rest = bytes - bound.Symbols - bound.Operations;

        Console.WriteLine();

        Console.WriteLine(string.Create(
            Invariant,
            $"Where a run of {pairs} pairs of the first shape allocates, per pair: {bound.Symbols / pairs:N0} B for "
            + $"the declared symbols, {bound.Operations / pairs:N0} B for the operations of the constructors, "
            + $"{rest / pairs:N0} B for the parsers and the pipeline."));
    }

    private static (double Cpu, long Bytes, bool Cached) Rerun(Func<int, int, string> pair, int pairs)
    {
        var compilation = Compile(pair, pairs);
        var driver = Subject.CreateDriver().RunGenerators(compilation);
        List<double> cpu = [];
        List<long> bytes = [];
        var cached = true;

        for (var edit = 0; edit < Edits; edit++)
        {
            var edited = Edited(compilation, pairs, edit);
            var cpuBefore = Environment.CpuUsage.TotalTime;
            var bytesBefore = GC.GetAllocatedBytesForCurrentThread();

            driver = driver.RunGenerators(edited);

            bytes.Add(GC.GetAllocatedBytesForCurrentThread() - bytesBefore);
            cpu.Add((Environment.CpuUsage.TotalTime - cpuBefore).TotalMilliseconds);

            cached &= driver.GetRunResult().Results[0].TrackedOutputSteps.Values
                .SelectMany(steps => steps)
                .SelectMany(step => step.Outputs)
                .All(output => output.Reason == IncrementalStepRunReason.Cached);
        }

        bytes.Sort();
        return (cpu.Min(), bytes[bytes.Count / 2], cached);
    }

    private static (long Symbols, long Operations) Bind(Compilation compilation)
    {
        var declarations = compilation.SyntaxTrees
            .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            .Where(declaration => declaration.BaseList?.Types.Any(IsToolsetOrAgent) is true)
            .ToList();

        var start = GC.GetAllocatedBytesForCurrentThread();

        var types = declarations
            .Select(declaration => compilation.GetSemanticModel(declaration.SyntaxTree).GetDeclaredSymbol(declaration)!)
            .ToList();

        var symbols = GC.GetAllocatedBytesForCurrentThread();

        _ = types
            .SelectMany(type => type.InstanceConstructors)
            .Where(constructor => !constructor.IsImplicitlyDeclared)
            .SelectMany(constructor => constructor.DeclaringSyntaxReferences)
            .Select(reference => reference.GetSyntax())
            .Count(syntax => compilation.GetSemanticModel(syntax.SyntaxTree).GetOperation(syntax) is not null);

        return (symbols - start, GC.GetAllocatedBytesForCurrentThread() - symbols);
    }

    // As the generator's syntax check, for the names the sources use
    private static bool IsToolsetOrAgent(BaseTypeSyntax named) => named.Type.ToString() is "Toolset" or "Agent";

    private static Compilation Compile(Func<int, int, string> pair, int pairs) =>
        Subject.Compile([.. Enumerable.Range(0, pairs).Select(index => pair(index, pairs)), ScaleSources.Unrelated]);

    // The unrelated file is the last tree
    private static Compilation Edited(Compilation compilation, int pairs, int edit) =>
        Subject.Edit(compilation, pairs, ScaleSources.Unrelated.Replace("Value + 1", $"Value + {edit + 2}"));
}
