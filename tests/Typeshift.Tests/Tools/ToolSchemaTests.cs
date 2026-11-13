using Typeshift.Generated;
using Typeshift.Tests.Tools.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Tools;

public class ToolSchemaTests : UnitTestBase
{
    private ToolJson Json { get; } = new([], []);

    [Fact]
    public void Given_A_Struct_Parameter_Defaulting_To_Default_Its_Schema_Carries_The_Default()
    {
        // Act
        var locate = SchemaToolset.CreateLocate(Json);

        // Assert
        locate.JsonSchema.Assert.ParameterDefault("point", """{"latitude":0,"longitude":0}""");
    }

    [Fact]
    public void Given_An_Undocumented_JsonElement_Parameter_Its_Schema_Accepts_Any_Json()
    {
        // Act
        var echo = SchemaToolset.CreateEchoJson(Json);

        // Assert
        echo.JsonSchema.Assert.ParameterSchema("value", "{}");
    }

    [Fact]
    public void Given_A_Polymorphic_Parameter_Each_Case_Of_Its_Schema_Names_Its_Type()
    {
        // Act
        var draw = ShapeToolset.CreateDraw(Json);

        // Assert
        draw.JsonSchema.Assert.ParameterCaseTypes("shape", "object", "object");
    }

    [Fact]
    public void Given_A_Nullable_Polymorphic_Parameter_Each_Case_Of_Its_Schema_Accepts_Null()
    {
        // Act
        var draw = ShapeToolset.CreateDraw(Json);

        // Assert
        draw.JsonSchema.Assert.ParameterCasesAcceptNull("outline");
    }

    [Fact]
    public void Given_A_Nullable_Reference_Parameter_It_Is_Typed_As_Nullable()
    {
        // Act
        var search = SchemaToolset.CreateSearchCompanies(Json);

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
        var search = SchemaToolset.CreateSearchCompanies(Json);

        // Assert
        search.JsonSchema.Assert.MatchesWithoutDescriptions(reflected.JsonSchema);
    }

    [Fact]
    public void Given_A_Recursive_Parameter_Type_The_Schema_Matches_AIFunctionFactory()
    {
        // Arrange
        var reflected = AIFunctionFactory.Create(TreeToolset.CountCategories, new AIFunctionFactoryOptions
        {
            SerializerOptions = Json.SerializerOptions
        });

        // Act
        var count = TreeToolset.CreateCountCategories(Json);

        // Assert
        count.JsonSchema.Assert.MatchesWithoutDescriptions(reflected.JsonSchema);
    }

    [Fact]
    public void Given_A_Parameter_Type_Json_Cannot_Hold_The_Error_Names_The_Tool_And_The_Parameter()
    {
        // Arrange
        var report = BindingToolset.CreateReport(Json);

        // Act
        var schema = () => report.JsonSchema;

        // Assert
        schema.Should()
            .Throw<NotSupportedException>()
            .WithMessage("The parameter 'lookup' of the tool 'report'*'exception.targetSite'*[JsonIgnore]*");
    }

    [Fact]
    public void Given_A_Parameter_Json_Cannot_Hold_Itself_The_Error_Says_To_Use_Another_Type()
    {
        // Arrange
        var wait = BindingToolset.CreateWait(Json);

        // Act
        var schema = () => wait.JsonSchema;

        // Assert
        schema.Should()
            .Throw<NotSupportedException>()
            .WithMessage("The parameter 'handle' of the tool 'wait'*IntPtr*use another type*");
    }

    [Fact]
    public void Given_Param_Docs_They_Become_The_Parameter_Descriptions()
    {
        // Act
        var search = SchemaToolset.CreateSearchCompanies(Json);

        // Assert
        search.JsonSchema.Assert.ParameterDescription("query", "What to search for.");
    }

    [Fact]
    public void Given_Two_Tools_Each_Gets_A_Schema_Of_Its_Own()
    {
        // Arrange
        _ = SchemaToolset.CreateLocate(Json).JsonSchema;

        // Act
        var count = SchemaToolset.CreateCountWords(Json).JsonSchema;

        // Assert
        count.Assert.Properties("text");
    }

    [Fact]
    public void Given_Parameters_The_Call_Fills_They_Stay_Out_Of_The_Schema()
    {
        // Act
        var probe = ProbeToolset.CreateProbe(new ProbeSettings(), Json);

        // Assert
        probe.JsonSchema.Assert.Properties("query");
    }

    [Fact]
    public void Given_No_WithDescription_The_Summary_Is_The_Tool_Description()
    {
        // Act
        var count = SchemaToolset.CreateCountWords(Json);

        // Assert
        count.Description.Should().Be("Counts the words of a text.");
    }

    [Fact]
    public void Given_An_Async_Method_Without_WithName_The_Tool_Is_Named_Without_The_Suffix()
    {
        // Act
        var count = SchemaToolset.CreateCountWords(Json);

        // Assert
        count.Name.Should().Be("count_words");
    }

    [Fact]
    public void Given_WithName_The_Tool_Goes_By_That_Name()
    {
        // Act
        var echo = SchemaToolset.CreateEchoJson(Json);

        // Assert
        echo.Name.Should().Be("echo_json");
    }
}
