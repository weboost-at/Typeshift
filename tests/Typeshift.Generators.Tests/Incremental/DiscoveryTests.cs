using Typeshift.Generators.Parsing;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;

namespace Typeshift.Generators.Tests.Incremental;

// The syntax predicates decide which declarations of an edited file are bound again. One that passes too much binds
// on every keystroke, and its output stays the same, so only a test of the predicate itself sees it; one that passes
// too little drops a toolset, an agent or a JSON context
public class DiscoveryTests : GeneratorTestBase
{
    [Theory]
    [InlineData("class Tools : Toolset;")]
    [InlineData("class Tools : Typeshift.Toolset;")]
    [InlineData("class Tools : global::Typeshift.Toolset;")]
    [InlineData("partial class Tools : System.IDisposable, Toolset;")]
    public void Given_A_Class_Whose_Base_List_Names_Toolset_It_Is_Bound_As_A_Toolset(string declaration)
    {
        // Arrange
        var node = SyntaxFactory.ParseMemberDeclaration(declaration)!;

        // Act
        var bound = Discovery.IsToolset(node, Ct);

        // Assert
        bound.Should().BeTrue();
    }

    [Theory]
    [InlineData("class Tools;")]
    [InlineData("class Tools : Agent;")]
    [InlineData("class Tools : ToolsetBase;")]
    [InlineData("abstract class Tools : Toolset;")]
    [InlineData("record Tools : Toolset;")]
    public void Given_Any_Other_Declaration_It_Is_Not_Bound_As_A_Toolset(string declaration)
    {
        // Arrange
        var node = SyntaxFactory.ParseMemberDeclaration(declaration)!;

        // Act
        var bound = Discovery.IsToolset(node, Ct);

        // Assert
        bound.Should().BeFalse();
    }

    [Theory]
    [InlineData("class Help : Agent;")]
    [InlineData("class Help : Typeshift.Agent;")]
    [InlineData("partial class Help : System.IDisposable, global::Typeshift.Agent;")]
    public void Given_A_Class_Whose_Base_List_Names_Agent_It_Is_Bound_As_An_Agent(string declaration)
    {
        // Arrange
        var node = SyntaxFactory.ParseMemberDeclaration(declaration)!;

        // Act
        var bound = Discovery.IsAgent(node, Ct);

        // Assert
        bound.Should().BeTrue();
    }

    [Theory]
    [InlineData("class Help;")]
    [InlineData("class Help : Toolset;")]
    [InlineData("abstract class Help : Agent;")]
    [InlineData("interface IHelp : Agent;")]
    public void Given_Any_Other_Declaration_It_Is_Not_Bound_As_An_Agent(string declaration)
    {
        // Arrange
        var node = SyntaxFactory.ParseMemberDeclaration(declaration)!;

        // Act
        var bound = Discovery.IsAgent(node, Ct);

        // Assert
        bound.Should().BeFalse();
    }

    [Theory]
    [InlineData("partial class Json : JsonSerializerContext;")]
    [InlineData("partial class Json() : System.Text.Json.Serialization.JsonSerializerContext(null);")]
    public void Given_A_Class_Whose_Base_List_Names_JsonSerializerContext_It_Is_Bound_As_A_Json_Context(
        string declaration)
    {
        // Arrange
        var node = SyntaxFactory.ParseMemberDeclaration(declaration)!;

        // Act
        var bound = Discovery.IsJsonContext(node, Ct);

        // Assert
        bound.Should().BeTrue();
    }

    [Theory]
    [InlineData("partial class Json;")]
    [InlineData("partial class Json : Toolset;")]
    [InlineData("abstract partial class Json : JsonSerializerContext;")]
    public void Given_Any_Other_Declaration_It_Is_Not_Bound_As_A_Json_Context(string declaration)
    {
        // Arrange
        var node = SyntaxFactory.ParseMemberDeclaration(declaration)!;

        // Act
        var bound = Discovery.IsJsonContext(node, Ct);

        // Assert
        bound.Should().BeFalse();
    }
}
