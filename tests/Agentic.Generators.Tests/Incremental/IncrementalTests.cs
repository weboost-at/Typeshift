using Agentic.Generators.Tests.Incremental.Support;
using Agentic.Generators.Tests.Toolsets.Support;

namespace Agentic.Generators.Tests.Incremental;

public class IncrementalTests : GeneratorTestBase
{
    [Fact]
    public void Given_An_Unrelated_Edit_Every_Step_Is_Cached()
    {
        // Arrange
        var compilation = Compile(ToolsetSources.WebResearch, IncrementalSources.Unrelated);
        var first = Run(compilation);

        // Act
        var second = RunAgain(first, Edit(compilation, 1, IncrementalSources.UnrelatedEdited));

        // Assert
        second.Assert.Cached(TrackingNames.Classes, TrackingNames.Toolsets);
    }

    [Fact]
    public void Given_An_Unrelated_Edit_Every_Output_Is_Cached()
    {
        // Arrange
        var compilation = Compile(ToolsetSources.WebResearch, IncrementalSources.Unrelated);
        var first = Run(compilation);

        // Act
        var second = RunAgain(first, Edit(compilation, 1, IncrementalSources.UnrelatedEdited));

        // Assert
        second.Assert.OutputsCached();
    }

    [Fact]
    public void Given_An_Edit_To_A_Toolset_Its_Output_Is_Regenerated()
    {
        // Arrange
        var compilation = Compile(ToolsetSources.WebResearch, IncrementalSources.Unrelated);
        var first = Run(compilation);
        var edited = ToolsetSources.WebResearch.Replace("Forgets what was read.", "Clears what was read.");

        // Act
        var second = RunAgain(first, Edit(compilation, 0, edited));

        // Assert
        second.Assert.OutputsModified();
    }
}
