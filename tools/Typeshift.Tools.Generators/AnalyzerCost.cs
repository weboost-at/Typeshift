using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Typeshift.Generators.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Typeshift.Tools.Generators;

// What the analyzer costs in a project's code that uses nothing of the library: it is asked about every call and
// every class, and almost none of them is one of the library's
internal static class AnalyzerCost
{
    private const int Rounds = 7;

    private const int Classes = 2_000;

    private const int Callers = 20;

    private const int MethodsPerCaller = 10;

    private const int CallsPerMethod = 500;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private const string Toolset =
        """
        using Typeshift;

        namespace Big;

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public static async Task Print()
    {
        var compilation = Subject.Compile(GetCalls(), GetClasses(), Toolset);

        Console.WriteLine();

        Console.WriteLine(string.Create(
            Invariant,
            $"The analyzer over {Callers * MethodsPerCaller * CallsPerMethod:N0} calls of other assemblies and "
            + $"{Classes:N0} classes that use nothing of the library,"));

        Console.WriteLine(
            $"beside one toolset: the least time of {Rounds} runs in its own code, as the compiler counts it, and the "
            + "median");

        Console.WriteLine(
            "allocation of the whole analysis, against an analyzer that is asked about every call and does nothing.");

        Console.WriteLine();
        Console.WriteLine("  analyzer    own ms   allocated MB");

        // The first analysis pays for the JIT
        await Analyze(new TypeshiftAnalyzer(), compilation.Clone());

        await Print("Typeshift", () => new TypeshiftAnalyzer(), compilation);
        await Print("nothing", () => new NoAnalyzer(), compilation);
    }

    private static async Task Print(string name, Func<DiagnosticAnalyzer> create, Compilation compilation)
    {
        List<double> times = [];
        List<double> megabytes = [];

        // A compilation binds once, so each run gets one that has bound nothing yet
        for (var round = 0; round < Rounds; round++)
        {
            var (time, bytes) = await Analyze(create(), compilation.Clone());

            times.Add(time.TotalMilliseconds);
            megabytes.Add(bytes / 1024d / 1024d);
        }

        megabytes.Sort();

        Console.WriteLine(string.Create(Invariant, $"  {name,-10} {times.Min(),7:F1} {megabytes[Rounds / 2],14:F1}"));
    }

    private static async Task<(TimeSpan Time, long Bytes)> Analyze(DiagnosticAnalyzer analyzer, Compilation compilation)
    {
        var analysis = compilation.WithAnalyzers(
            [analyzer],
            new CompilationWithAnalyzersOptions(
                new AnalyzerOptions([]),
                onAnalyzerException: null,
                concurrentAnalysis: false,
                logAnalyzerExecutionTime: true));

        var before = GC.GetTotalAllocatedBytes(precise: true);
        await analysis.GetAnalyzerDiagnosticsAsync(CancellationToken.None);
        var bytes = GC.GetTotalAllocatedBytes(precise: true) - before;
        var telemetry = await analysis.GetAnalyzerTelemetryInfoAsync(analyzer, CancellationToken.None);

        return (telemetry.ExecutionTime, bytes);
    }

    private static string GetCalls()
    {
        var source = new StringBuilder("using System.Collections.Generic;\nusing System.Text;\n\nnamespace Big;\n");

        for (var caller = 0; caller < Callers; caller++)
        {
            source.AppendLine($"public class Caller{caller}\n{{");

            for (var method = 0; method < MethodsPerCaller; method++)
            {
                source.AppendLine($"    public void Call{method}(StringBuilder text, List<int> list)\n    {{");

                for (var call = 0; call < CallsPerMethod; call++)
                {
                    source.AppendLine(call % 2 == 0 ? $"        text.Append({call});" : $"        list.Add({call});");
                }

                source.AppendLine("    }");
            }

            source.AppendLine("}");
        }

        return source.ToString();
    }

    private static string GetClasses()
    {
        var source = new StringBuilder("namespace Big;\n\npublic class Root;\n\npublic class Branch : Root;\n");

        for (var index = 0; index < Classes; index++)
        {
            source.AppendLine($"public class Leaf{index} : Branch {{ public int Value {{ get; set; }} }}");
        }

        return source.ToString();
    }

#pragma warning disable RS1001 // The tool runs it itself; no compiler loads it
    private sealed class NoAnalyzer : DiagnosticAnalyzer
#pragma warning restore RS1001
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [
            new("NONE001", "Nothing", "Nothing", "Nothing", DiagnosticSeverity.Warning, isEnabledByDefault: true)
        ];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterOperationAction(
                operation => _ = ((IInvocationOperation)operation.Operation).TargetMethod.Name.Length,
                OperationKind.Invocation);
        }
    }
}
