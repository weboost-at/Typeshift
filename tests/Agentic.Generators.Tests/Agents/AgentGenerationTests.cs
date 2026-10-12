using Agentic.Generators.Tests.Agents.Support;
using Agentic.Generators.Tests.Toolsets.Support;

namespace Agentic.Generators.Tests.Agents;

public class AgentGenerationTests : GeneratorTestBase
{
    [Fact]
    public void Given_An_Agent_Its_Settings_Tool_Settings_And_Runtime_Members_Are_Generated()
    {
        // Act
        var run = Run(ToolsetSources.WebResearch, AgentSources.Profiling);

        // Assert
        Snapshot.Verify(run.Result);
    }

    [Fact]
    public void Given_An_Agent_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ToolsetSources.WebResearch, AgentSources.Profiling);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_An_Agent_Using_A_Toolset_Of_A_Reference_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Arrange
        var tools = CompileReference("Demo", ToolsetSources.WebResearch);

        // Act
        var run = Run(Compile("App", [tools], AgentSources.UsesWebResearch));

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_Tools_Named_Like_Members_Of_The_View_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(AgentSources.Lookup, AgentSources.ReservedToolNames);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Public_Agent_Of_An_Internal_Toolset_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(AgentSources.Lookup, AgentSources.PublicAgentOfInternalToolset);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Public_Agent_In_An_Internal_Class_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(AgentSources.Lookup, AgentSources.PublicAgentInInternalClass);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_Nested_Agents_Sharing_A_Name_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(AgentSources.Lookup, AgentSources.NestedTwins);

        // Assert
        run.Assert.CompilesCleanly();
    }
}
