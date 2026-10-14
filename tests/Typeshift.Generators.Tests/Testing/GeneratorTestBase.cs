using System.Collections.Immutable;
using Typeshift.Generators.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Typeshift.Generators.Tests.Testing;

public abstract class GeneratorTestBase
{
    protected const string SubjectAssembly = "Subject";

    protected static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default
        .WithLanguageVersion(LanguageVersion.Latest)
        .WithDocumentationMode(DocumentationMode.Diagnose)
        .WithFeatures([new("InterceptorsNamespaces", "Typeshift.Generated")]);

    private static readonly ImmutableArray<MetadataReference> FrameworkReferences = CreateFrameworkReferences();

    private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers =
        [new TypeshiftAnalyzer(), new ParameterDocsSuppressor()];

    protected CancellationToken Ct { get; } = TestContext.Current.CancellationToken;

    protected static CSharpCompilation Compile(params string[] sources) => Compile(SubjectAssembly, [], sources);

    protected static CSharpCompilation Compile(
        string assemblyName,
        IEnumerable<MetadataReference> references,
        params string[] sources) =>
        CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: sources.Select((source, index) => Parse(source, $"/{assemblyName}/Source{index}.cs")),
            references: FrameworkReferences.AddRange(references),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

    protected static SyntaxTree Parse(string source, string path) =>
        CSharpSyntaxTree.ParseText(source, ParseOptions, path);

    protected static GeneratorDriver CreateDriver(
        CSharpParseOptions? parseOptions = null,
        BuildProperties? properties = null) =>
        CSharpGeneratorDriver.Create(
            generators: [new TypeshiftGenerator().AsSourceGenerator()],
            parseOptions: parseOptions ?? ParseOptions,
            optionsProvider: properties ?? BuildProperties.None,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

    // The generator, then the analyzers over the subject and the generated code, as the IDE runs them
    protected GeneratorRun Run(
        Compilation compilation,
        CSharpParseOptions? parseOptions = null,
        BuildProperties? properties = null)
    {
        var driver = CreateDriver(parseOptions, properties)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _, Ct);

        return new GeneratorRun(driver, output, Analyze(output, properties ?? BuildProperties.None));
    }

    protected GeneratorRun Run(params string[] sources) => Run(Compile(sources));

    // Runs the same driver again, as the IDE does after an edit
    protected GeneratorRun RunAgain(GeneratorRun previous, Compilation edited)
    {
        var driver = previous.Driver.RunGeneratorsAndUpdateCompilation(edited, out var output, out _, Ct);
        return new GeneratorRun(driver, output, Analyze(output, BuildProperties.None));
    }

    protected static Compilation Edit(Compilation compilation, int tree, string source) =>
        compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.ElementAt(tree),
            Parse(source, compilation.SyntaxTrees.ElementAt(tree).FilePath));

    // As a project without GenerateDocumentationFile: the compiler keeps /// comments as plain comments
    protected GeneratorRun RunWithoutDocumentation(params string[] sources)
    {
        var parseOptions = ParseOptions.WithDocumentationMode(DocumentationMode.None);
        var compilation = Compile(sources);

        var undocumented = compilation.SyntaxTrees.Aggregate(
            compilation,
            (current, tree) => current.ReplaceSyntaxTree(tree, tree.WithRootAndOptions(tree.GetRoot(), parseOptions)));

        return Run(undocumented, parseOptions);
    }

    // An assembly compiled with the generator, referenced from the subject as metadata
    protected MetadataReference CompileReference(string assemblyName, params string[] sources) =>
        Emit(Run(Compile(assemblyName, [], sources)).Output);

    protected MetadataReference CompileReference(
        string assemblyName,
        BuildProperties properties,
        params string[] sources) =>
        Emit(Run(Compile(assemblyName, [], sources), properties: properties).Output);

    // An assembly whose project does not run the generator, as one without the package reference
    protected MetadataReference CompileReferenceWithoutGenerator(string assemblyName, params string[] sources) =>
        Emit(Compile(assemblyName, [], sources));

    // Compiler diagnostics included, so the suppressor can act on them; suppressed ones are reported as such
    private IReadOnlyList<Diagnostic> Analyze(Compilation output, BuildProperties properties) =>
        output
            .WithAnalyzers(
                Analyzers,
                new CompilationWithAnalyzersOptions(
                    new AnalyzerOptions([], properties),
                    onAnalyzerException: null,
                    concurrentAnalysis: false,
                    logAnalyzerExecutionTime: false,
                    reportSuppressedDiagnostics: true))
            .GetAllDiagnosticsAsync(Ct)
            .GetAwaiter()
            .GetResult();

    private MetadataReference Emit(Compilation compilation)
    {
        using var image = new MemoryStream();
        var emitted = compilation.Emit(image, cancellationToken: Ct);

        if (!emitted.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, emitted.Diagnostics));

        return MetadataReference.CreateFromImage(image.ToArray());
    }

    private static ImmutableArray<MetadataReference> CreateFrameworkReferences()
    {
        var trusted = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;

        return
        [
            .. trusted
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => MetadataReference.CreateFromFile(path))
        ];
    }
}
