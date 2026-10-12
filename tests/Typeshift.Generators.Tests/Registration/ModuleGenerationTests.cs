using Typeshift.Generators.Tests.Registration.Support;

namespace Typeshift.Generators.Tests.Registration;

public class ModuleGenerationTests : GeneratorTestBase
{
    [Fact]
    public void Given_A_Toolset_And_An_Agent_The_Module_Registers_And_Stamps_Both()
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
}
