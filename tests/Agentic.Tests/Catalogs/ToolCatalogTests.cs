using Agentic.Catalogs;
using Agentic.Tests.Toolsets.Companies;
using Agentic.Tools;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Tests.Catalogs;

public class ToolCatalogTests : HostTestBase
{
    private ToolCatalog Catalog => Services.GetRequiredService<ToolCatalog>();

    [Fact]
    public void Given_The_Name_Of_A_Tool_Of_A_Referenced_Assembly_It_Creates_That_Tool()
    {
        // Act
        var tool = Catalog.CreateTool(new ToolName("find_companies"));

        // Assert
        tool.Name.Should().Be("find_companies");
    }

    [Fact]
    public async Task Given_Overrides_The_Created_Tool_Runs_With_Them()
    {
        // Arrange
        var overrides = new CompanyToolset.FindCompaniesOverrides { Limit = Data.Tool.Limit };
        var tool = Catalog.CreateTool(CompanyToolset.Tools.FindCompanies, overrides);

        // Act
        var result = await tool.InvokeAsync(Arguments(("query", Data.Tool.Query)), Ct);

        // Assert
        ReadResult<FindCompaniesSettings>(result).Should().Be(new FindCompaniesSettings { Limit = Data.Tool.Limit.Default });
    }
}
