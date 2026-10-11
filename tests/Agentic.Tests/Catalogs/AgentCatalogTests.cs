using Agentic.Catalogs;
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
}
