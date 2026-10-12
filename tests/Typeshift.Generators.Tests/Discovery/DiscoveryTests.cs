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
}
