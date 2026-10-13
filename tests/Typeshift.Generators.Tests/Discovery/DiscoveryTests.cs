using FluentAssertions;

namespace Typeshift.Generators.Tests.Discovery;

public class DiscoveryTests : GeneratorTestBase
{
    [Fact]
    public void Given_Classes_That_Derive_Neither_Toolset_Nor_AgentDefinition_Nothing_Is_Generated()
    {
        // Act
        var run = Run(
            """
            namespace Demo;

            public class Plain;

            public class Derived : System.Exception;
            """);

        // Assert
        run.HintNames.Should().BeEmpty();
    }

    [Fact]
    public void Given_A_Toolset_Naming_Its_Base_On_Two_Parts_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(
            """
            using Typeshift.Toolsets;

            namespace Demo;

            internal partial class EchoToolset : Toolset
            {
                public EchoToolset() => AddTool(Echo).WithName("echo").WithDescription("Echoes the text.");
            }

            internal partial class EchoToolset : Toolset
            {
                public static string Echo(string text) => text;
            }
            """);

        // Assert
        run.Assert.CompilesCleanly();
    }
}
