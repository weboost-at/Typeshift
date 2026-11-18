using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using Typeshift.Generators.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Typeshift.Tools.Generators;

// Code as it stands while it is typed: every source the generator tests compile and every docs sample, with each
// constructor cut after each of its tokens. An exception in the generator drops every generated file of a project
// (CS8785) and one in the analyzer is AD0001, so neither may throw on any of the cuts. A cut that fails becomes an
// entry of HalfTypedSources, with its fix
internal static class Fuzz
{
    private const string Samples = "samples/Typeshift.Samples.Docs";

    private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers =
        [new TypeshiftAnalyzer(), new ParameterDocsSuppressor()];

    public static int Run(int threads)
    {
        var sources = GetCatalogSources().Concat(GetSamples()).ToList();
        var cuts = sources.SelectMany(Cut).DistinctBy(cut => cut.Text).ToList();
        var failures = new ConcurrentBag<string>();
        var done = 0;

        Console.WriteLine($"{sources.Count} sources, {cuts.Count} cuts, on {threads} threads");

        Parallel.ForEach(cuts, new ParallelOptions { MaxDegreeOfParallelism = threads }, cut =>
        {
            if (GetFailure(cut.Text) is { } failure)
                failures.Add($"{cut.Name}\n{failure}\n-----\n{cut.Text}\n=====");

            if (Interlocked.Increment(ref done) % 1000 == 0) Console.WriteLine($"{done} of {cuts.Count}");
        });

        foreach (var failure in failures.Order(StringComparer.Ordinal))
        {
            Console.WriteLine();
            Console.WriteLine(failure);
        }

        Console.WriteLine();
        Console.WriteLine($"{failures.Count} of {cuts.Count} cuts made the generator or the analyzer throw");
        return failures.IsEmpty ? 0 : 1;
    }

    // What the generator or the analyzer said of its own failure, or the exception that left the run
    private static string? GetFailure(string source)
    {
        try
        {
            var driver = Subject.CreateDriver()
                .RunGeneratorsAndUpdateCompilation(Subject.Compile(source), out var output, out _);

            var reported = output
                .WithAnalyzers(Analyzers)
                .GetAllDiagnosticsAsync()
                .GetAwaiter()
                .GetResult()
                .Where(diagnostic => diagnostic.Id == "AD0001");

            var failed = driver.GetRunResult().Diagnostics.Concat(reported).Select(diagnostic => diagnostic.ToString());
            var text = string.Join("\n", failed);

            return text.Length > 0 ? text : null;
        }
        catch (Exception exception)
        {
            return exception.ToString();
        }
    }

    // A source with one of its constructors ended after each token of the constructor's body
    private static IEnumerable<(string Name, string Text)> Cut((string Name, string Text) source)
    {
        var root = CSharpSyntaxTree.ParseText(source.Text).GetRoot();

        foreach (var constructor in root.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
        {
            var (start, end) = constructor switch
            {
                { Body: { } body } => (body.OpenBraceToken.Span.End, body.CloseBraceToken.SpanStart),
                { ExpressionBody: { } arrow } => (arrow.Expression.SpanStart, arrow.Expression.Span.End),
                _ => (0, 0)
            };

            foreach (var token in constructor.DescendantTokens())
            {
                if (token.SpanStart < start || token.Span.End > end) continue;

                yield return (
                    $"{source.Name} @ {token.Span.End}",
                    string.Concat(source.Text.AsSpan(0, token.Span.End), source.Text.AsSpan(end)));
            }
        }
    }

    // Every constant of the classes named ...Sources that this project compiles from the generator tests
    private static IEnumerable<(string Name, string Text)> GetCatalogSources() =>
        typeof(Fuzz).Assembly.GetTypes()
            .Where(type => type.IsPublic && type.Name.EndsWith("Sources", StringComparison.Ordinal))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field is { IsLiteral: true } && field.FieldType == typeof(string))
            .Select(field => ($"{field.DeclaringType!.Name}.{field.Name}", (string)field.GetRawConstantValue()!));

    private static IEnumerable<(string Name, string Text)> GetSamples()
    {
        var root = GetRepository();

        return Directory.EnumerateFiles(Path.Combine(root, Samples), "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !path.Contains("/bin/") && !path.Contains("/obj/"))
            .Order(StringComparer.Ordinal)
            .Select(path => (path, File.ReadAllText(Path.Combine(root, path))));
    }

    private static string GetRepository()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);

        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Typeshift.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName
            ?? throw new InvalidOperationException("Run the tool from its place in the repository: no Typeshift.slnx "
                + $"is above {AppContext.BaseDirectory}.");
    }
}
