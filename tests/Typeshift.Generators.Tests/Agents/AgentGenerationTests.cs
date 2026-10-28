using Typeshift.Generators.Tests.Agents.Support;
using Typeshift.Generators.Tests.Diagnostics.Support;
using Typeshift.Generators.Tests.Toolsets.Support;
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
    public void Given_Tools_Of_The_Agents_Own_They_Are_Generated_In_It_And_Offered_After_The_Toolsets()
    {
        // Act
        var run = Run(AgentSources.Tickets, AgentSources.OwnTools);

        // Assert
        Snapshot.Verify(run.Result);
    }

    [Fact]
    public void Given_Tools_Of_The_Agents_Own_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(AgentSources.Tickets, AgentSources.OwnTools);

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
    public void Given_A_Reference_Of_Docs_Only_The_Interceptor_Registers_Its_Module()
    {
        // Arrange
        var reference = CompileReference("Contracts", ToolsetSources.SchemaAnnotations);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.EmptyHost));

        // Assert
        run.Source("Typeshift.Generated.AddTypeshiftInterceptor.g.cs")
            .Should()
            .Contain("global::Typeshift.Generated.ContractsModule.Register(services);");
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

    [Theory]
    [InlineData("Demo.Provider", "global::Demo.Provider", "Provider.LmStudio")]
    [InlineData("Guid", "global::System.Guid", "System.Guid.Empty")]
    [InlineData("int", "int", "7")]
    public void Given_A_Provider_Id_Type_The_Alias_The_Settings_And_The_Core_Services_Use_It(
        string property,
        string type,
        string provider)
    {
        // Arrange
        var compilation = Compile(AgentSources.Routed(type, provider));

        // Act
        var run = Run(compilation, properties: BuildProperties.WithProviderIdType(property));

        var sources = run.Result.GeneratedTrees.Select(tree => tree.ToString()).ToList();

        // Assert
        run.Assert.CompilesCleanly();
        sources.Should().Contain(source => source.Contains($"global::Typeshift.IChatClientProvider<{type}>;"));
        sources.Should().Contain(source => source.Contains($"public {type}? Provider {{ get; init; }}"));
        sources.Should().Contain(source => source.Contains($"public {type}? SummaryProvider {{ get; init; }}"));
        sources.Should().Contain(source => source.Contains($"AddCoreServices<global::System.Guid, {type}>"));
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
