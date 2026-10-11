using Agentic.Tests.Agents.Support;
using Agentic.Tests.Toolsets.Support;
using FluentAssertions;

namespace Agentic.Tests.Agents;

public class AgentDefinitionTests : UnitTestBase
{
    [Fact]
    public void Given_Selected_Tools_And_A_Whole_Toolset_The_Agent_Offers_Exactly_Those_In_Order()
    {
        // Act
        var tools = new ProbeAgent().Tools.Select(tool => tool.Name.Value);

        // Assert
        tools.Should().Equal("find_companies", "create_company_profile", "probe");
    }

    [Fact]
    public void Given_A_Method_Of_Another_Toolset_Selecting_It_Throws_An_ArgumentException()
    {
        // Act
        var define = () => new MisselectingAgent<ProbeToolset>();

        // Assert
        define.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Given_A_Tool_The_Agent_Already_Offers_Adding_It_Again_Throws_An_InvalidOperationException()
    {
        // Act
        var define = () => new DuplicatingAgent<ProbeToolset>();

        // Assert
        define.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Given_Settings_No_Tool_Of_The_Agent_Reads_WithToolSettings_Throws_An_ArgumentException()
    {
        // Act
        var define = () => new MissettingAgent<ProbeSettings>();

        // Assert
        define.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Given_A_Name_That_Is_Not_Lower_Snake_Case_WithUniqueName_Throws_An_ArgumentException()
    {
        // Act
        var define = () => new NamedAgent<object>("Probe Agent");

        // Assert
        define.Should().Throw<ArgumentException>();
    }
}
