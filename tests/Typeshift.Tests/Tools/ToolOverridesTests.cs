using Typeshift.Tests.Tools.Support;
using FluentAssertions;

namespace Typeshift.Tests.Tools;

public class ToolOverridesTests : UnitTestBase
{
    [Fact]
    public void Given_A_Value_The_Overrides_Apply_It_Over_The_Settings_Below()
    {
        // Arrange
        var overrides = new ProbeToolset.ProbeOverrides { Limit = Data.Tool.Limit.Updated };

        // Act
        var settings = overrides.ApplyTo(new ProbeSettings { Limit = Data.Tool.Limit.Default });

        // Assert
        settings.Limit.Should().Be(Data.Tool.Limit.Updated);
    }

    [Fact]
    public void Given_A_Null_Value_The_Settings_Below_Keep_Theirs()
    {
        // Arrange
        var overrides = new ProbeToolset.ProbeOverrides();

        // Act
        var settings = overrides.ApplyTo(new ProbeSettings { Limit = Data.Tool.Limit.Default });

        // Assert
        settings.Limit.Should().Be(Data.Tool.Limit.Default);
    }

    [Fact]
    public void Given_A_Member_That_Is_Not_Configurable_The_Overrides_Have_No_Property_For_It()
    {
        // Act
        var properties = typeof(ProbeToolset.ProbeOverrides).GetProperties().Select(property => property.Name);

        // Assert
        properties.Should().Equal("Limit");
    }
}
