using Agentic.Agents;
using Agentic.Catalogs;
using Agentic.Tests.Agents.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Tests.Catalogs;

public class AgentCatalogTests : HostTestBase
{
    private AgentCatalog Catalog => Services.GetRequiredService<AgentCatalog>();

    [Fact]
    public void Lists_Every_Code_Agent_By_Unique_Name()
    {
        // Act
        var names = Catalog.CodeAgents.Select(agent => agent.UniqueName);

        // Assert
        names.Should().BeEquivalentTo("probe_agent", "research_agent");
    }

    [Fact]
    public void Given_Two_Agents_With_One_Unique_Name_It_Throws_An_InvalidOperationException()
    {
        // Arrange
        AgentDefinition[] agents = [new NamedAgent<string>("twin_agent"), new NamedAgent<int>("twin_agent")];

        // Act
        var index = () => new AgentCatalog(agents);

        // Assert
        index.Should().Throw<InvalidOperationException>();
    }
}
