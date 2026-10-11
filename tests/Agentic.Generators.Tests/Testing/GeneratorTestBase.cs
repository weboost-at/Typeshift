using System.Collections.Immutable;
using Agentic.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Agentic.Generators.Tests.Testing;

public abstract class GeneratorTestBase
{
    protected const string SubjectAssembly = "Subject";

    protected static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default
        .WithLanguageVersion(LanguageVersion.Latest)
        .WithDocumentationMode(DocumentationMode.Diagnose)
        .WithFeatures([new("InterceptorsNamespaces", "Agentic.Generated")]);

    private static readonly ImmutableArray<MetadataReference> FrameworkReferences = CreateFrameworkReferences();

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

    protected static GeneratorDriver CreateDriver() =>
        CSharpGeneratorDriver.Create(
            generators: [new AgentGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

    protected GeneratorRun Run(Compilation compilation)
    {
        var driver = CreateDriver().RunGeneratorsAndUpdateCompilation(compilation, out var output, out _, Ct);
        return new GeneratorRun(driver, output);
    }

    protected GeneratorRun Run(params string[] sources) => Run(Compile(sources));

    // Runs the same driver again, as the IDE does after an edit
    protected GeneratorRun RunAgain(GeneratorRun previous, Compilation edited)
    {
        var driver = previous.Driver.RunGeneratorsAndUpdateCompilation(edited, out var output, out _, Ct);
        return new GeneratorRun(driver, output);
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

        var driver = CSharpGeneratorDriver
            .Create([new AgentGenerator().AsSourceGenerator()], parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(undocumented, out var output, out _, Ct);

        return new GeneratorRun(driver, output);
    }

    // An assembly compiled with the generator, referenced from the subject as metadata
    protected MetadataReference CompileReference(string assemblyName, params string[] sources) =>
        CompileReference(assemblyName, [], sources);

    protected MetadataReference CompileReference(
        string assemblyName,
        IEnumerable<MetadataReference> references,
        params string[] sources)
    {
        var run = Run(Compile(assemblyName, references, sources));
        using var image = new MemoryStream();
        var emitted = run.Output.Emit(image, cancellationToken: Ct);

        if (!emitted.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, emitted.Diagnostics));

        return MetadataReference.CreateFromImage(image.ToArray());
    }

    // An assembly whose project does not run the generator, as one without the package reference
    protected MetadataReference CompileReferenceWithoutGenerator(string assemblyName, params string[] sources)
    {
        using var image = new MemoryStream();
        var emitted = Compile(assemblyName, [], sources).Emit(image, cancellationToken: Ct);

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
