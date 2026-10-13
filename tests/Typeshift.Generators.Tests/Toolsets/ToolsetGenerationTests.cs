using Typeshift.Generators.Tests.Toolsets.Support;

namespace Typeshift.Generators.Tests.Toolsets;

public class ToolsetGenerationTests : GeneratorTestBase
{
    [Fact]
    public void Given_A_Toolset_Its_Tools_Settings_And_Functions_Are_Generated()
    {
        // Act
        var run = Run(ToolsetSources.WebResearch);

        // Assert
        Snapshot.Verify(run.Result);
    }

    [Fact]
    public void Given_A_Toolset_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ToolsetSources.WebResearch);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Nested_Toolset_Its_Containing_Class_Is_Reopened()
    {
        // Act
        var run = Run(ToolsetSources.Nested);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Multi_Line_Description_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ToolsetSources.MultiLineDescription);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Parameter_Documented_Twice_The_Generator_Does_Not_Fail()
    {
        // Act
        var run = Run(ToolsetSources.DocumentedTwice);

        // Assert
        run.Assert.DoesNotReport("CS8785");
    }

    [Fact]
    public void Given_Names_Of_Members_Of_The_Generated_Types_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ToolsetSources.ReservedNames);

        // Assert
        run.Assert.CompilesCleanly();
    }
}
