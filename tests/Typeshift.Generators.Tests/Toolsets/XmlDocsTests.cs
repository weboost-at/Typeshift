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
