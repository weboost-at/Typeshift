using System.Reflection;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Toolsets.Companies;
using Typeshift.Tests.Toolsets.Support;
using FluentAssertions;

namespace Typeshift.Tests.Agents;

public class AgentSettingsTests : AgentTestBase
{
    [Fact]
    public void Exposes_Only_The_Model_The_Configurable_Chat_Options_And_The_Configurable_Tools()
    {
        // Act
        var members = typeof(ProbeAgent.Settings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.Name);

        // Assert
        members.Should()
            .BeEquivalentTo("Model", "Temperature", "MaxOutputTokens", "Instructions", "FindCompanies", "Probe");
    }

    [Fact]
    public void Given_Settings_Without_Values_They_Serialize_As_An_Empty_Object()
    {
        // Act
        var json = new ProbeAgent.Settings().Serialize();

        // Assert
        json.Should().Be("{}");
    }

    [Fact]
    public void Given_A_Value_For_One_Tool_Only_That_Tool_Is_Serialized()
    {
        // Arrange
        var settings = new ProbeAgent.Settings
        {
            FindCompanies = new CompanyToolset.FindCompaniesOverrides { Limit = Data.Tool.Limit }
        };

        // Act
        var json = settings.Serialize();

        // Assert
        json.Should().Be($$$$"""{"tools":{"find_companies":{"limit":{{{{Data.Tool.Limit.Default}}}}}}}""");
    }

    [Fact]
    public async Task Given_Settings_Saved_Under_An_Id_They_Load_Back_Equal()
    {
        // Arrange
        var settings = new ProbeAgent.Settings
        {
            Model = Agent.Model,
            Temperature = Agent.Temperature,
            Instructions = Agent.Instructions,
            FindCompanies = new CompanyToolset.FindCompaniesOverrides { Limit = Data.Tool.Limit }
        };

        await Runtime.SaveSettingsAsync(Agent.Id.Default, settings, Ct);

        // Act
        var loaded = await Runtime.LoadSettingsAsync(Agent.Id.Default, Ct);

        // Assert
        loaded.Should().Be(settings);
    }

    [Fact]
    public async Task Given_No_Settings_Saved_Under_An_Id_Loading_Returns_Empty_Settings()
    {
        // Act
        var loaded = await Runtime.LoadSettingsAsync(Agent.Id.Default, Ct);

        // Assert
        loaded.Should().Be(new ProbeAgent.Settings());
    }

    [Fact]
    public async Task Given_Values_Configured_Under_An_Id_The_Run_Resolves_With_Them()
    {
        // Arrange
        await Runtime.ConfigureAsync(Agent.Id.Default, settings => settings with
        {
            Temperature = Agent.Temperature,
            Instructions = Agent.Instructions,
            Probe = new ProbeToolset.ProbeOverrides { Limit = Data.Tool.Limit }
        }, Ct);

        // Act
        var options = await Runtime.ResolveChatOptionsAsync(Agent.Id.Default, ct: Ct);
        var probe = await ProbeAsync(options);

        // Assert
        options.Temperature.Should().Be(Agent.Temperature.Default);
        options.Instructions.Should().Be(Agent.Instructions);
        probe.Limit.Should().Be(Data.Tool.Limit.Default);
    }

    [Fact]
    public async Task Given_Settings_Under_Two_Ids_Each_Run_Resolves_With_Its_Own()
    {
        // Arrange
        await Runtime.ConfigureAsync(
            Agent.Id.Default, settings => settings with { Temperature = Agent.Temperature }, Ct);
        await Runtime.ConfigureAsync(
            Agent.Id.Updated, settings => settings with { Temperature = Agent.Temperature.Updated }, Ct);

        // Act
        var options = await Runtime.ResolveChatOptionsAsync(Agent.Id.Updated, ct: Ct);

        // Assert
        options.Temperature.Should().Be(Agent.Temperature.Updated);
    }
}
