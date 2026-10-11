using Agentic.Generators.Tests.Registration.Support;
using FluentAssertions;

namespace Agentic.Generators.Tests.Registration;

public class InterceptorGenerationTests : GeneratorTestBase
{
    [Fact]
    public void Given_AddAgentic_And_A_Referenced_Module_The_Interceptor_Registers_Both_Modules()
    {
        // Arrange
        var tools = CompileReference("Tools", RegistrationSources.EchoToolset);

        // Act
        var run = Run(Compile("App", [tools], RegistrationSources.EchoAgent, RegistrationSources.Host));

        // Assert
        Snapshot.Verify(run.Result);
    }

    [Fact]
    public void Given_AddAgentic_And_A_Referenced_Module_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Arrange
        var tools = CompileReference("Tools", RegistrationSources.EchoToolset);

        // Act
        var run = Run(Compile("App", [tools], RegistrationSources.EchoAgent, RegistrationSources.Host));

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_No_AddAgentic_Call_No_Interceptor_Is_Generated()
    {
        // Act
        var run = Run(RegistrationSources.EchoToolset);

        // Assert
        run.HintNames.Should().NotContain("Interceptors.g.cs");
    }

    [Fact]
    public void Given_InternalsVisibleTo_A_Generated_Project_The_Tests_Compile_Their_AddAgentic_Call()
    {
        // Arrange
        var app = CompileReference(
            "App",
            RegistrationSources.VisibleToTests,
            RegistrationSources.EchoToolset,
            RegistrationSources.Host);

        // Act
        var run = Run(Compile("Tests", [app], RegistrationSources.EchoAgent, RegistrationSources.TestHost));

        // Assert
        run.Assert.CompilesCleanly();
    }
}
