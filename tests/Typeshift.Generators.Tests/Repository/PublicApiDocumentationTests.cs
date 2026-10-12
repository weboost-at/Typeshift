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
            .OfType<IMethodSymbol>()
            .Where(HasResult)
            .Where(method => PublicApi.Docs(method).Element("returns") is null)
            .Select(method => method.ToDisplayString());

        // Assert
        gaps.Should().BeEmpty();
    }

    private static bool Inherits(ISymbol member) => PublicApi.Docs(member).Element("inheritdoc") is not null;

    private static IEnumerable<string> Tags(ISymbol member, string tag) =>
        PublicApi.Docs(member).Elements(tag).Select(element => (string?)element.Attribute("name") ?? "");

    private static IEnumerable<string> Parameters(ISymbol member) =>
        member switch
        {
            IMethodSymbol method => method.Parameters.Skip(method.IsExtensionMethod ? 1 : 0).Select(p => p.Name),
            IPropertySymbol property => property.Parameters.Select(parameter => parameter.Name),
            _ => []
        };

    private static IEnumerable<string> TypeParameters(ISymbol member) =>
        member switch
        {
            IMethodSymbol method => method.TypeParameters.Select(parameter => parameter.Name),
            INamedTypeSymbol type => type.TypeParameters.Select(parameter => parameter.Name),
            _ => []
        };

    // A Task or ValueTask without a value completes, it returns nothing a reader needs described
    private static bool HasResult(IMethodSymbol method) =>
        method is { MethodKind: MethodKind.Ordinary, ReturnsVoid: false }
        && method.ReturnType.ToDisplayString() is not ("System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask");
}
