using Typeshift.Generators.Tests.Agents.Support;
using Typeshift.Generators.Tests.Diagnostics.Support;
using FluentAssertions;

namespace Typeshift.Generators.Tests.Agents;

public class AgentGenerationTests : GeneratorTestBase
{
    [Fact]
    public void Given_An_Agent_Its_Settings_Declaration_Host_Members_Module_And_Interceptor_Are_Generated()
    {
        // Act
        var run = Run(AgentSources.Tickets, AgentSources.Support);

        // Assert
        Snapshot.Verify(run.Result);
    }

    [Fact]
    public void Given_An_Agent_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(AgentSources.Tickets, AgentSources.Support);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Toolset_Of_A_Reference_The_Agent_Configures_It_And_The_Interceptor_Registers_Both_Modules()
    {
        // Arrange
        var reference = CompileReference("Companies", ReferenceSources.CompanyToolset);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.CompanyAgent));

        // Assert
        Snapshot.Verify(run.Result);
    }

    [Fact]
    public void Given_A_Toolset_Of_A_Reference_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Arrange
        var reference = CompileReference("Companies", ReferenceSources.CompanyToolset);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.CompanyAgent));

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_Names_That_Would_Clash_The_Generated_Code_Avoids_Them_And_Compiles()
    {
        // Act
        var run = Run(AgentSources.Clashes);

        // Assert
        run.Assert.CompilesCleanly();
        run.Output.GetTypeByMetadataName("Demo.Outer+WeatherAgent+Settings")!
            .GetMembers("TemperatureTool")
            .Should()
            .ContainSingle();
    }

    [Fact]
    public void Given_A_Private_Nested_Agent_It_Gets_No_Host_Members_And_No_Registration()
    {
        // Act
        var run = Run(AgentSources.Private);

        // Assert
        run.Assert.GeneratesValidCode();
        run.Output.GetTypeByMetadataName("Demo.Outer_HiddenAgentHostExtensions").Should().BeNull();
    }

    [Fact]
    public void Given_An_Id_Type_The_Aliases_And_The_Host_Members_Use_It()
    {
        // Arrange
        var compilation = Compile(AgentSources.Tickets, AgentSources.Support);

        // Act
        var run = Run(compilation, properties: BuildProperties.WithIdType("int"));

        var sources = run.Result.GeneratedTrees.Select(tree => tree.ToString()).ToList();

        // Assert
        run.Assert.CompilesCleanly();
        sources.Should().Contain(source => source.Contains("global::Typeshift.ITypeshiftStore<int>;"));
        sources.Should().Contain(source => source.Contains("int? settingsId = null"));
    }
}
