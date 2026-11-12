using Typeshift.Generators.Tests.Agents.Support;
using Typeshift.Generators.Tests.Diagnostics.Support;
using Typeshift.Generators.Tests.Toolsets.Support;
using FluentAssertions;
using Microsoft.CodeAnalysis;

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
    public void Given_A_Caller_Outside_The_Agents_Namespace_Its_Host_Still_Offers_The_Typed_Members()
    {
        // Act
        var run = Run(AgentSources.Tickets, AgentSources.Support, AgentSources.CallerOutsideTheAgentsNamespace);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Caller_In_The_Agents_Namespace_Without_Typeshift_Its_Host_Offers_The_Typed_Members()
    {
        // Act
        var run = Run(AgentSources.Tickets, AgentSources.Support, AgentSources.CallerInTheAgentsNamespace);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_An_Agent_With_A_Nested_Type_Named_Host_Its_Host_Members_Are_Generated()
    {
        // Act
        var run = Run(AgentSources.NestedHost);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_Two_Agents_Whose_Namespaces_Join_Alike_Each_Gets_Host_Members_Of_Its_Own()
    {
        // Act
        var run = Run(AgentSources.UnderscoredNamespaces);

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
    public void Given_A_TypeshiftRuntime_Create_Call_The_Interceptor_Builds_The_Runtime_With_The_Projects_Module()
    {
        // Act
        var run = Run(AgentSources.Tickets, AgentSources.Support, AgentSources.Runtime);

        // Assert
        var interceptor = run.Source("Typeshift.Generated.AddTypeshiftInterceptor.g.cs");
        interceptor.Should().Contain("public static global::Typeshift.TypeshiftRuntime Create(");
        interceptor.Should().Contain("CreateRuntime(services, AddTypeshift(services), client, configure)");
        interceptor.Should().Contain("Module.Register(services);");
    }

    [Fact]
    public void Given_A_TypeshiftRuntime_Create_Call_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(AgentSources.Tickets, AgentSources.Support, AgentSources.Runtime);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Theory]
    [InlineData("TypeshiftRuntime")]
    [InlineData("Typeshift.TypeshiftRuntime")]
    [InlineData("global::Typeshift.TypeshiftRuntime")]
    public void Given_Only_A_TypeshiftRuntime_Create_Call_It_Is_Intercepted(string receiver)
    {
        // Act
        var run = Run(AgentSources.RuntimeOnly(receiver));

        // Assert
        run.Assert.Intercepts($"{receiver}.Create(client)");
    }

    [Fact]
    public void Given_Only_A_TypeshiftRuntime_Create_Call_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(AgentSources.RuntimeOnly("TypeshiftRuntime"));

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Create_Method_Of_The_Projects_Own_The_Generator_Does_Not_Intercept_It()
    {
        // Act
        var run = Run(AgentSources.OwnCreate);

        // Assert
        run.Assert.DoesNotIntercept("Factory.Create(client)");
    }

    [Fact]
    public void Given_A_TypeshiftRuntime_Create_Call_Bound_At_Run_Time_The_Generator_Does_Not_Intercept_It()
    {
        // Act
        var run = Run(AgentSources.DynamicRuntime);

        // Assert
        run.Assert.DoesNotIntercept("TypeshiftRuntime.Create(client)");
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
        run.Result.GeneratedTrees.Should().NotContain(tree => tree.FilePath.EndsWith("+Host.g.cs"));
    }

    [Theory]
    [InlineData("global::Demo.Provider", "Provider.LmStudio")]
    [InlineData("global::System.Guid", "System.Guid.Empty")]
    [InlineData("int", "7")]
    public void Given_A_Model_Of_A_Provider_Of_A_Type_The_Settings_Store_Providers_Of_That_Type(
        string type,
        string provider)
    {
        // Act
        var run = Run(AgentSources.Routed(type, provider));

        var sources = run.Result.GeneratedTrees.Select(tree => tree.ToString()).ToList();

        // Assert
        run.Assert.CompilesCleanly();
        sources.Should().Contain(source => source.Contains($"public {type}? Provider {{ get; init; }}"));
        sources.Should().Contain(source => source.Contains($"public {type}? SummaryProvider {{ get; init; }}"));
    }

    [Theory]
    [InlineData("Demo.VendorAgent+Settings", "Demo.Vendor?")]
    [InlineData("Demo.NamedAgent+Settings", "string?")]
    [InlineData("Demo.PickedAgent+Settings", "string?")]
    public void Given_Agents_Of_Two_Provider_Types_Each_Settings_Store_Providers_Of_Its_Own(
        string settings,
        string provider)
    {
        // Act
        var run = Run(AgentSources.TwoProviderIdTypes);

        // Assert
        run.Assert.CompilesCleanly();
        run.Output.GetTypeByMetadataName(settings)!
            .GetMembers("Provider")
            .OfType<IPropertySymbol>()
            .Single()
            .Type.ToDisplayString()
            .Should()
            .Be(provider);
    }
}
