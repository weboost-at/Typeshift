using System.Text.RegularExpressions;
using Typeshift.Generators.Tests.Repository.Support;
using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace Typeshift.Generators.Tests.Repository;

public class PublicApiDocumentationTests
{
    [Fact]
    public void Every_Documented_Member_Documents_Each_Of_Its_Parameters()
    {
        // Act
        var gaps = PublicApi.DocumentedMembers
            .Where(member => !Inherits(member))
            .SelectMany(member => Parameters(member)
                .Where(parameter => !Tags(member, "param").Contains(parameter))
                .Select(parameter => $"{member.ToDisplayString()}: {parameter}"));

        // Assert
        gaps.Should().BeEmpty();
    }

    [Fact]
    public void Every_Documented_Generic_Member_Documents_Each_Of_Its_Type_Parameters()
    {
        // Act
        var gaps = PublicApi.DocumentedMembers
            .Where(member => !Inherits(member))
            .SelectMany(member => TypeParameters(member)
                .Where(parameter => !Tags(member, "typeparam").Contains(parameter))
                .Select(parameter => $"{member.ToDisplayString()}: {parameter}"));

        // Assert
        gaps.Should().BeEmpty();
    }

    [Fact]
    public void Every_Documented_Method_With_A_Result_Documents_What_It_Returns()
    {
        // Act
        var gaps = PublicApi.DocumentedMembers
            .Where(member => !Inherits(member))
            .Where(member => Signature(member) is { } signature && HasResult(signature))
            .Where(member => PublicApi.Docs(member).Element("returns") is null)
            .Select(member => member.ToDisplayString());

        // Assert
        gaps.Should().BeEmpty();
    }

    [Fact]
    public void Given_A_Member_That_Throws_Its_Docs_Name_The_Exception()
    {
        // Act
        var gaps = ThrowingMembers.Undocumented;

        // Assert
        gaps.Should().BeEmpty(Listing.Of(gaps));
    }

    [Fact]
    public void Given_An_Exception_Of_The_Library_A_Member_Names_It_In_Its_Docs()
    {
        // Arrange
        var documented = string.Concat(
            RepositoryFiles.Find("*.cs", "src/Typeshift")
                .Concat(RepositoryFiles.Find("*.verified.txt", "tests/Typeshift.Generators.Tests/Snapshots"))
                .Select(RepositoryFiles.Read));

        // Act
        var unnamed = PublicApi.Members.OfType<INamedTypeSymbol>()
            .Where(IsException)
            .Where(type => !ExceptionTag(type).IsMatch(documented))
            .Select(type => type.Name);

        // Assert
        unnamed.Should().BeEmpty("a caller finds an exception in the docs of what throws it, generated members too");
    }

    [Fact]
    public void Every_Guide_Link_In_The_Xml_Docs_Reaches_A_Page_And_An_Anchor()
    {
        // Act
        var dead = GuideLinks.Dead;

        // Assert
        dead.Should().BeEmpty(Listing.Of(dead));
    }

    private static bool IsException(INamedTypeSymbol type) =>
        type.BaseType is { } parent && (parent.ToDisplayString() == "System.Exception" || IsException(parent));

    // The tag of a library member, which names the type alone, or of a generated one, which names it in full
    private static Regex ExceptionTag(INamedTypeSymbol type) =>
        new($@"<exception cref=""(global::Typeshift\.)?{type.Name}""");

    private static bool Inherits(ISymbol member) => PublicApi.Docs(member).Element("inheritdoc") is not null;

    private static IEnumerable<string> Tags(ISymbol member, string tag) =>
        PublicApi.Docs(member).Elements(tag).Select(element => (string?)element.Attribute("name") ?? "");

    private static IEnumerable<string> Parameters(ISymbol member) =>
        member switch
        {
            IMethodSymbol method => method.Parameters.Skip(method.IsExtensionMethod ? 1 : 0).Select(p => p.Name),
            IPropertySymbol property => property.Parameters.Select(parameter => parameter.Name),
            INamedTypeSymbol { DelegateInvokeMethod: { } invoke } => invoke.Parameters.Select(p => p.Name),
            _ => []
        };

    private static IEnumerable<string> TypeParameters(ISymbol member) =>
        member switch
        {
            IMethodSymbol method => method.TypeParameters.Select(parameter => parameter.Name),
            INamedTypeSymbol type => type.TypeParameters.Select(parameter => parameter.Name),
            _ => []
        };

    // A delegate is documented as the method it stands for
    private static IMethodSymbol? Signature(ISymbol member) =>
        member switch
        {
            IMethodSymbol { MethodKind: MethodKind.Ordinary } method => method,
            INamedTypeSymbol { DelegateInvokeMethod: { } invoke } => invoke,
            _ => null
        };

    // A Task or ValueTask without a value completes, it returns nothing a reader needs described
    private static bool HasResult(IMethodSymbol method) =>
        !method.ReturnsVoid
        && method.ReturnType.ToDisplayString()
            is not ("System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask");
}
