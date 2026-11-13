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

    // Every warning wave is on, as the SDK builds a consumer's project; a later wave that warns about generated code
    // is fixed there, never by lowering the level
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
                nullableContextOptions: NullableContextOptions.Enable,
                warningLevel: 9999));

    // As a project with AllowUnsafeBlocks
    protected static CSharpCompilation CompileUnsafe(params string[] sources)
    {
        var compilation = Compile(sources);
        return compilation.WithOptions(compilation.Options.WithAllowUnsafe(true));
    }

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

        return Checked(new GeneratorRun(driver, output, Analyze(output, properties ?? BuildProperties.None)));
    }

    protected GeneratorRun Run(params string[] sources) => Run(Compile(sources));

    // Runs the same driver again, as the IDE does after an edit
    protected GeneratorRun RunAgain(GeneratorRun previous, Compilation edited)
    {
        var driver = previous.Driver.RunGeneratorsAndUpdateCompilation(edited, out var output, out _, Ct);
        return Checked(new GeneratorRun(driver, output, Analyze(output, BuildProperties.None)));
    }

    protected static Compilation Edit(Compilation compilation, int tree, string source) =>
        compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.ElementAt(tree),
            Parse(source, compilation.SyntaxTrees.ElementAt(tree).FilePath));

    // As a project that pins <LangVersion>
    protected GeneratorRun RunAs(LanguageVersion version, params string[] sources)
    {
        var parseOptions = ParseOptions.WithLanguageVersion(version);
        var compilation = Compile(sources);

        // All at once: a compilation takes no trees of two language versions
        var pinned = compilation.SyntaxTrees.Select(tree => tree.WithRootAndOptions(tree.GetRoot(), parseOptions));

        return Run(compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(pinned), parseOptions);
    }

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

    // Compiler diagnostics included, so the suppressor can act on them; suppressed ones are reported as such. The
    // analyzer enables concurrent execution, so it runs concurrently here as in a build
    private IReadOnlyList<Diagnostic> Analyze(Compilation output, BuildProperties properties) =>
        output
            .WithAnalyzers(
                Analyzers,
                new CompilationWithAnalyzersOptions(
                    new AnalyzerOptions([], properties),
                    onAnalyzerException: null,
                    concurrentAnalysis: true,
                    logAnalyzerExecutionTime: false,
                    reportSuppressedDiagnostics: true))
            .GetAllDiagnosticsAsync(Ct)
            .GetAwaiter()
            .GetResult();

    // What every run asserts, whatever its test goes on to. It reached its end: a failed generator leaves no file to
    // hold an error and an analyzer that threw reports nothing, so every other assertion on such a run would pass.
    // And its files start with the header, so no snapshot has to pin it
    private static GeneratorRun Checked(GeneratorRun run) =>
        run.Assert.RunsToTheEnd().Assert.StartsEveryFileWithTheHeader();

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
