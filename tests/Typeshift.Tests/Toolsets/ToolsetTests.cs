using Typeshift.Tests.Toolsets.Support;
using FluentAssertions;

namespace Typeshift.Tests.Toolsets;

public class ToolsetTests : HostTestBase
{
    [Fact]
    public void Given_A_Tool_With_Settings_Its_Schema_Holds_Only_The_Model_Facing_Parameters()
    {
        // Act
        var probe = new ProbeToolset().CreateTools(Json).Single();

        // Assert
        probe.JsonSchema.Assert.Properties("query");
    }

    [Fact]
    public void Given_Configurable_Settings_The_Overrides_Hold_Every_Member_But_The_Excluded()
    {
        // Act
        var members = typeof(ProbeToolset.ProbeOverrides).GetProperties().Select(property => property.Name);

        // Assert
        members.Should().BeEquivalentTo(nameof(ProbeSettings.Limit));
    }

    [Fact]
    public async Task Given_No_Settings_The_Tool_Runs_With_Its_Code_Defaults()
    {
        // Arrange
        var probe = new ProbeToolset().CreateTools(Json).Single();

        // Act
        var result = await probe.InvokeAsync(Arguments(("query", Data.Tool.Query)), Ct);

        // Assert
        ReadResult<ProbeResult>(result).Settings.Should().Be(new ProbeSettings());
    }

    [Fact]
    public async Task Given_Overrides_The_Tool_Runs_With_Them_Over_Its_Code_Defaults()
    {
        // Arrange
        var overrides = new ProbeToolset.ProbeOverrides { Limit = Data.Tool.Limit };
        var probe = new ProbeToolset().CreateTools(Json, overrides).Single();

        // Act
        var result = await probe.InvokeAsync(Arguments(("query", Data.Tool.Query)), Ct);

        // Assert
        ReadResult<ProbeResult>(result).Settings.Should().Be(new ProbeSettings { Limit = Data.Tool.Limit.Default });
    }
}
