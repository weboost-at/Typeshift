using Typeshift.Tests.Tools.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Tools;

public class ToolSchemaTests : HostTestBase
{
    [Fact]
    public void Given_A_Struct_Parameter_Defaulting_To_Default_Its_Schema_Carries_The_Default()
    {
        // Act
        var locate = new SchemaToolset().CreateTools(Json).Single(tool => tool.Name == "locate");

        // Assert
        locate.JsonSchema.Assert.ParameterDefault("point", """{"latitude":0,"longitude":0}""");
    }

    [Fact]
    public void Given_An_Undocumented_JsonElement_Parameter_Its_Schema_Accepts_Any_Json()
    {
        // Act
        var echo = new SchemaToolset().CreateTools(Json).Single(tool => tool.Name == "echo");

        // Assert
        echo.JsonSchema.Assert.ParameterSchema("value", "{}");
    }

    [Fact]
    public void Given_A_Nullable_Reference_Parameter_It_Is_Typed_As_Nullable()
    {
        // Act
        var search = new SchemaToolset().CreateTools(Json).Single(tool => tool.Name == "search_companies");

        // Assert
        search.JsonSchema.Assert.ParameterTypes("country", "string", "null");
    }

    [Fact]
    public void Given_The_Same_Method_The_Schema_Matches_AIFunctionFactory_Without_Descriptions()
    {
        // Arrange
        var reflected = AIFunctionFactory.Create(SchemaToolset.SearchCompanies, new AIFunctionFactoryOptions
        {
            SerializerOptions = Json.SerializerOptions
        });

        // Act
        var search = new SchemaToolset().CreateTools(Json).Single(tool => tool.Name == "search_companies");

        // Assert
        search.JsonSchema.Assert.MatchesWithoutDescriptions(reflected.JsonSchema);
    }

    [Fact]
    public void Given_Param_Docs_They_Become_The_Parameter_Descriptions()
    {
        // Act
        var search = new SchemaToolset().CreateTools(Json).Single(tool => tool.Name == "search_companies");

        // Assert
        search.JsonSchema.Assert.ParameterDescription("query", "What to search for.");
    }

    [Fact]
    public void Given_No_WithDescription_The_Summary_Is_The_Tool_Description()
    {
        // Act
        var count = new SchemaToolset().CreateTools(Json).Single(tool => tool.Name == "count_words");

        // Assert
        count.Description.Should().Be("Counts the words of a text.");
    }
}
