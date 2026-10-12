using Typeshift.Generators.Tests.Incremental.Support;
using Typeshift.Generators.Tests.Registration.Support;
using Typeshift.Generators.Tests.Toolsets.Support;

namespace Typeshift.Generators.Tests.Incremental;

public class IncrementalTests : GeneratorTestBase
{
    [Fact]
    public void Given_An_Unrelated_Edit_Every_Step_Is_Cached()
    {
        // Arrange
        var compilation = Compile(
            ToolsetSources.WebResearch,
            IncrementalSources.Unrelated,
            RegistrationSources.EchoToolset,
            RegistrationSources.EchoAgent,
            RegistrationSources.Host);

        var first = Run(compilation);

        // Act
        var second = RunAgain(first, Edit(compilation, 1, IncrementalSources.UnrelatedEdited));

        // Assert
        second.Assert.Cached(
            TrackingNames.Classes,
            TrackingNames.Toolsets,
            TrackingNames.Agents,
            TrackingNames.ResolvedAgents,
            TrackingNames.Module,
            TrackingNames.References,
            TrackingNames.Calls);
    }

    [Fact]
    public void Given_An_Unrelated_Edit_Every_Output_Is_Cached()
    {
        // Arrange
        var compilation = Compile(
            ToolsetSources.WebResearch,
            IncrementalSources.Unrelated,
            RegistrationSources.EchoToolset,
            RegistrationSources.EchoAgent,
            RegistrationSources.Host);

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

    [Fact]
    public void Given_An_Edit_To_A_Tool_Name_The_Dependent_Agent_Is_Regenerated()
    {
        // Arrange
        var compilation = Compile(RegistrationSources.EchoToolset, RegistrationSources.EchoAgent);
        var first = Run(compilation);
        var renamed = RegistrationSources.EchoToolset.Replace("WithName(\"echo\")", "WithName(\"repeat\")");

        // Act
        var second = RunAgain(first, Edit(compilation, 0, renamed));

        // Assert
        second.Assert.Modified(TrackingNames.ResolvedAgents);
    }

    [Fact]
    public void Given_A_Toolset_Moved_Down_Its_File_The_Module_And_The_Agents_Stay_Cached()
    {
        // Arrange
        var compilation = Compile(RegistrationSources.EchoToolset, RegistrationSources.EchoAgent);
        var first = Run(compilation);

        // Act
        var second = RunAgain(first, Edit(compilation, 0, IncrementalSources.Moved(RegistrationSources.EchoToolset)));

        // Assert
        second.Assert.Cached(TrackingNames.Module, TrackingNames.ResolvedAgents);
    }
}
