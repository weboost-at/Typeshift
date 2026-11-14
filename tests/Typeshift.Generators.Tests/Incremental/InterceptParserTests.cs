using Typeshift.Generators.Parsing;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;

namespace Typeshift.Generators.Tests.Incremental;

// The syntax predicate of the interceptor: the calls of an edited file the generator binds again. The symbol decides
// afterwards, so a predicate that passes every call writes the same code and only binds more
public class InterceptParserTests : GeneratorTestBase
{
    [Theory]
    [InlineData("services.AddTypeshift()")]
    [InlineData("builder.Services.AddTypeshift()")]
    [InlineData("TypeshiftRuntime.Create(client)")]
    [InlineData("Typeshift.TypeshiftRuntime.Create(client)")]
    [InlineData("global::Typeshift.TypeshiftRuntime.Create(client)")]
    public void Given_A_Call_Spelled_Like_One_The_Generator_Intercepts_It_Is_Bound(string call)
    {
        // Arrange
        var node = SyntaxFactory.ParseExpression(call);

        // Act
        var bound = InterceptParser.IsCandidate(node, Ct);

        // Assert
        bound.Should().BeTrue();
    }

    [Theory]
    [InlineData("Factory.Create(client)")]
    [InlineData("runtime.Create(client)")]
    [InlineData("Create(client)")]
    [InlineData("TypeshiftRuntime.Build(client)")]
    [InlineData("services.AddTypeshiftCore()")]
    [InlineData("services.AddTypeshift")]
    public void Given_Any_Other_Expression_It_Is_Not_Bound(string expression)
    {
        // Arrange
        var node = SyntaxFactory.ParseExpression(expression);

        // Act
        var bound = InterceptParser.IsCandidate(node, Ct);

        // Assert
        bound.Should().BeFalse();
    }
}
