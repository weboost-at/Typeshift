using Typeshift.Generators.Tests.Registration.Support;
using FluentAssertions;

namespace Typeshift.Generators.Tests.Registration;

public class ModuleGenerationTests : GeneratorTestBase
{
    [Fact]
    public void Given_A_Toolset_And_An_Agent_The_Module_Registers_Both()
    {
        // Act
        var run = Run(RegistrationSources.EchoToolset, RegistrationSources.EchoAgent);

        // Assert
        Snapshot.Verify(run.Result);
    }

    [Fact]
    public void Given_A_Toolset_And_An_Agent_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(RegistrationSources.EchoToolset, RegistrationSources.EchoAgent);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Json_Context_Of_A_Tool_Parameter_Type_The_Module_Registers_It()
    {
        // Act
        var run = Run(RegistrationSources.LookupToolset, RegistrationSources.QueryJsonContext);

        // Assert
        run.Source("Module.g.cs").Should().Contain("AddJsonContext(services, global::Tools.QueryJsonContext.Default);");
    }

    [Fact]
    public void Given_A_Json_Context_Of_An_Awaited_Tool_Result_Type_The_Module_Registers_It()
    {
        // Act
        var run = Run(RegistrationSources.LookupToolset, RegistrationSources.EntriesJsonContext);

        // Assert
        run.Source("Module.g.cs")
            .Should()
            .Contain("AddJsonContext(services, global::Tools.EntriesJsonContext.Default);");
    }

    [Fact]
    public void Given_A_Json_Context_Of_No_Tool_Type_The_Module_Leaves_It_Out()
    {
        // Act
        var run = Run(RegistrationSources.LookupToolset, RegistrationSources.UriJsonContext);

        // Assert
        run.Source("Module.g.cs").Should().NotContain("UriJsonContext");
    }

    [Fact]
    public void Given_A_Private_Json_Context_The_Module_Leaves_It_Out()
    {
        // Act
        var run = Run(RegistrationSources.LookupToolset, RegistrationSources.PrivateJsonContext);

        // Assert
        run.Source("Module.g.cs").Should().NotContain("PrivateJsonContext");
    }

    [Fact]
    public void Given_Json_Contexts_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(
            RegistrationSources.LookupToolset,
            RegistrationSources.QueryJsonContext,
            RegistrationSources.EntriesJsonContext);

        // Assert
        run.Assert.CompilesCleanly();
    }
}
