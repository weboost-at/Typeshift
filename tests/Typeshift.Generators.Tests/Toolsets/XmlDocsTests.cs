using Typeshift.Generators.Parsing;
using Typeshift.Generators.Tests.Agents.Support;
using Typeshift.Generators.Tests.Diagnostics.Support;
using Typeshift.Generators.Tests.Repository.Support;
using Typeshift.Generators.Tests.Toolsets.Support;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Typeshift.Generators.Tests.Toolsets;

// D3: the parsers read a comment of the compilation where it is written, which costs a fraction of the compiler's
// XML, and the XML where the comment alone does not say what the compiler writes. Every description has to read the
// same either way, so each documented declaration of a catalog is read both ways
public class XmlDocsTests : GeneratorTestBase
{
    [Theory]
    [InlineData(typeof(ShapeSources))]
    [InlineData(typeof(DiagnosticSources))]
    [InlineData(typeof(ToolsetSources))]
    [InlineData(typeof(AgentSources))]
    [InlineData(typeof(ReferenceSources))]
    public void Given_The_Comments_Of_A_Catalog_Read_Where_They_Are_Written_They_Read_As_The_Compilers_Xml(
        Type catalog)
    {
        // Arrange
        var documented = SourceCatalogs.NamesOf(catalog)
            .Select(name => Compile(SourceCatalogs.Read(catalog, name)))
            .SelectMany(GetDocumented)
            .ToList();

        // Act
        var read = documented
            .Select(symbol => (Symbol: symbol, Written: XmlDocs.ReadWritten(symbol, Ct)))
            .Where(reading => reading.Written is not null)
            .Select(reading => (
                reading.Symbol,
                Written: Show(reading.Written!),
                Compiled: Show(XmlDocs.ReadCompiled(reading.Symbol, Ct))))
            .ToList();

        var differences = read
            .Where(reading => reading.Written != reading.Compiled)
            .Select(reading => $"{reading.Symbol}: '{reading.Written}', the compiler's XML '{reading.Compiled}'")
            .ToList();

        // Assert
        read.Should().NotBeEmpty();
        differences.Should().BeEmpty(Listing.Of(differences));
    }

    [Theory]
    [InlineData(typeof(ShapeSources))]
    [InlineData(typeof(DiagnosticSources))]
    [InlineData(typeof(ToolsetSources))]
    [InlineData(typeof(AgentSources))]
    [InlineData(typeof(ReferenceSources))]
    public void Given_The_Comments_Of_A_Catalog_Without_Generated_Docs_They_Read_As_The_Compilers_Xml(Type catalog)
    {
        // Arrange
        var sources = SourceCatalogs.NamesOf(catalog).Select(name => SourceCatalogs.Read(catalog, name)).ToList();

        // Act
        var read = sources
            .SelectMany(source => Pair(Compile(source), CompileWithoutDocumentation(source)))
            .Where(pair => XmlDocs.ReadPlain(pair.Undocumented, Ct) is not null)
            .Where(pair => !XmlDocs.ReadUnparsed(pair.Undocumented, Ct).Contains("cref="))
            .Select(pair => (
                pair.Documented,
                Plain: Show(XmlDocs.Read(pair.Undocumented, Ct)),
                Compiled: Show(XmlDocs.ReadCompiled(pair.Documented, Ct))))
            .ToList();

        var differences = read
            .Where(reading => reading.Plain != reading.Compiled)
            .Select(reading => $"{reading.Documented}: '{reading.Plain}', the compiler's XML '{reading.Compiled}'")
            .ToList();

        // Assert
        read.Should().NotBeEmpty();
        differences.Should().BeEmpty(Listing.Of(differences));
    }

    [Fact]
    public void Given_Xml_Docs_Turned_Off_A_Cref_Reads_As_It_Is_Written()
    {
        // Arrange
        var find = CompileWithoutDocumentation(ShapeSources.DocsOfEveryKind)
            .GetTypeByMetadataName("Demo.DocumentedToolset")!
            .GetMembers("Find")
            .Single();

        // Act
        var docs = XmlDocs.Read(find, Ct);

        // Assert
        docs.Summary.Should().Be("Finds a string in a List of Numbers, as Count or Count do.");
    }

    // Each documented symbol of a compilation with generated docs, with the same symbol of one without
    private IEnumerable<(ISymbol Documented, ISymbol Undocumented)> Pair(Compilation documented, Compilation plain)
    {
        var symbols = GetDeclared(plain).ToDictionary(symbol => symbol.ToDisplayString(), StringComparer.Ordinal);

        return GetDocumented(documented)
            .Where(symbol => symbols.ContainsKey(symbol.ToDisplayString()))
            .Select(symbol => (symbol, symbols[symbol.ToDisplayString()]));
    }

    // The methods, properties and types a compilation declares, whose comments it keeps as plain comments
    private IEnumerable<ISymbol> GetDeclared(Compilation compilation) =>
        compilation.SyntaxTrees
            .SelectMany(tree => tree.GetRoot(Ct)
                .DescendantNodes()
                .Where(node => node is MethodDeclarationSyntax or PropertyDeclarationSyntax
                    or BaseTypeDeclarationSyntax)
                .Select(node => compilation.GetSemanticModel(tree).GetDeclaredSymbol(node, Ct)))
            .OfType<ISymbol>()
            .GroupBy(symbol => symbol.ToDisplayString(), StringComparer.Ordinal)
            .Select(group => group.First());

    // The methods, properties and types of the compilation that carry a /// or /** */ comment
    private IEnumerable<ISymbol> GetDocumented(Compilation compilation) =>
        compilation.SyntaxTrees
            .SelectMany(tree => tree.GetRoot(Ct)
                .DescendantNodes()
                .Where(node => node is MethodDeclarationSyntax or PropertyDeclarationSyntax
                    or BaseTypeDeclarationSyntax)
                .Where(node => node.GetLeadingTrivia().Any(trivia => trivia.HasStructure))
                .Select(node => compilation.GetSemanticModel(tree).GetDeclaredSymbol(node, Ct)))
            .OfType<ISymbol>();

    private static string Show(XmlDocs docs) =>
        string.Join(
            " | ",
            docs.Parameters
                .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
                .Select(parameter => $"{parameter.Key}: {parameter.Value}")
                .Prepend(docs.Summary ?? "(no summary)"));
}
