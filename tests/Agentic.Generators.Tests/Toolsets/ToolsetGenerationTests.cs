using Agentic.Generators.Tests.Toolsets.Support;

namespace Agentic.Generators.Tests.Toolsets;

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
}
