using Agentic.Generators.Tests.Agents.Support;
using Agentic.Generators.Tests.Toolsets.Support;

namespace Agentic.Generators.Tests.Agents;

public class AgentGenerationTests : GeneratorTestBase
{
    [Fact]
    public void Given_An_Agent_Its_Name_Tools_Allowlist_And_Configuration_View_Are_Generated()
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
}
