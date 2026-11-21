using System.Collections.Immutable;
using Typeshift.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Typeshift.Tools.Generators;

// A project as the generator tests compile one: the framework, the library, documentation on
internal static class Subject
{
    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default
        .WithLanguageVersion(LanguageVersion.Latest)
        .WithDocumentationMode(DocumentationMode.Diagnose)
        .WithFeatures([new("InterceptorsNamespaces", "Typeshift.Generated")]);

    private static readonly ImmutableArray<MetadataReference> References =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path))
    ];

    public static CSharpCompilation Compile(params string[] sources) =>
        CSharpCompilation.Create(
            "Subject",
            syntaxTrees: sources.Select((source, index) => Parse(source, $"/Subject/Source{index}.cs")),
            references: References,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                warningLevel: 9999));

    public static Compilation Edit(Compilation compilation, int tree, string source) =>
        compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.ElementAt(tree),
            Parse(source, compilation.SyntaxTrees.ElementAt(tree).FilePath));

    public static GeneratorDriver CreateDriver() =>
        CSharpGeneratorDriver.Create(
            generators: [new TypeshiftGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

    private static SyntaxTree Parse(string source, string path) =>
        CSharpSyntaxTree.ParseText(source, ParseOptions, path);
}
