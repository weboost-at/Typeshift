using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Toolsets.Companies;
using Typeshift.Tests.Toolsets.Support;
using FluentAssertions;

namespace Typeshift.Tests.Settings;

public class SettingsLayeringTests : AgentTestBase
{
    [Fact]
    public async Task Given_Stored_Tool_Defaults_And_No_Code_Settings_The_Defaults_Apply()
    {
        // Arrange
        await ToolDefaults.SaveAsync(Agent.Id.Default, new CompanyToolset.FindCompaniesOverrides { Limit = Data.Tool.Limit }, Ct);
        var options = await Runtime.ResolveChatOptionsAsync(Agent.Id.Default, ct: Ct);

        // Act
        var settings = await FindCompaniesAsync(options);

        // Assert
        settings.Limit.Should().Be(Data.Tool.Limit.Default);
    }

    [Fact]
    public async Task Given_Tool_Defaults_Stored_Under_Another_Id_They_Do_Not_Apply()
    {
        // Arrange
        await ToolDefaults.SaveAsync(Agent.Id.Updated, new CompanyToolset.FindCompaniesOverrides { Limit = Data.Tool.Limit }, Ct);
        var options = await Runtime.ResolveChatOptionsAsync(Agent.Id.Default, ct: Ct);

        // Act
        var settings = await FindCompaniesAsync(options);

        // Assert
        settings.Limit.Should().Be(new FindCompaniesSettings().Limit);
    }

    [Fact]
    public async Task Given_Stored_Tool_Defaults_And_Stored_Agent_Settings_The_Agent_Settings_Win()
    {
        // Arrange
        await ToolDefaults.SaveAsync(
            Agent.Id.Default, new CompanyToolset.FindCompaniesOverrides { Limit = Data.Tool.Limit }, Ct);

        await Runtime.ConfigureAsync(Agent.Id.Default, settings => settings with
        {
            FindCompanies = new CompanyToolset.FindCompaniesOverrides { Limit = Data.Tool.Limit.Updated }
        }, Ct);

        var options = await Runtime.ResolveChatOptionsAsync(Agent.Id.Default, ct: Ct);

        // Act
        var settings = await FindCompaniesAsync(options);

        // Assert
        settings.Limit.Should().Be(Data.Tool.Limit.Updated);
    }

    [Fact]
    public async Task Given_Stored_Tool_Defaults_And_Code_Settings_Of_The_Agent_The_Defaults_Do_Not_Apply()
    {
        // Arrange
        await ToolDefaults.SaveAsync(Agent.Id.Default, new ProbeToolset.ProbeOverrides { Limit = Data.Tool.Limit }, Ct);
        var options = await Runtime.ResolveChatOptionsAsync(Agent.Id.Default, ct: Ct);

        // Act
        var settings = await ProbeAsync(options);

        // Assert
        settings.Limit.Should().Be(5);
    }

    [Fact]
    public async Task Given_Stored_Agent_Settings_They_Beat_The_Code_Settings_For_Configurable_Members_Only()
    {
        // Arrange
        await Runtime.ConfigureAsync(Agent.Id.Default, settings => settings with
        {
            Probe = new ProbeToolset.ProbeOverrides { Limit = Data.Tool.Limit }
        }, Ct);

        var options = await Runtime.ResolveChatOptionsAsync(Agent.Id.Default, ct: Ct);

        // Act
        var settings = await ProbeAsync(options);

        // Assert
        settings.Should().Be(new ProbeSettings { Limit = Data.Tool.Limit.Default, Mode = "thorough" });
    }

    [Fact]
    public async Task Given_Run_Settings_They_Beat_The_Stored_Settings()
    {
        // Arrange
        await Runtime.ConfigureAsync(Agent.Id.Default, settings => settings with
        {
            Probe = new ProbeToolset.ProbeOverrides { Limit = Data.Tool.Limit }
        }, Ct);

        var run = new ProbeAgent.Settings
        {
            Probe = new ProbeToolset.ProbeOverrides { Limit = Data.Tool.Limit.Updated }
        };

        var options = await Runtime.ResolveChatOptionsAsync(Agent.Id.Default, run, ct: Ct);

        // Act
        var settings = await ProbeAsync(options);

        // Assert
        settings.Should().Be(new ProbeSettings { Limit = Data.Tool.Limit.Updated, Mode = "thorough" });
    }

    [Fact]
    public async Task Given_Stored_And_Run_Chat_Options_The_Run_Options_Win()
    {
        // Arrange
        await Runtime.ConfigureAsync(
            Agent.Id.Default, settings => settings with { Temperature = Agent.Temperature }, Ct);

        var run = new ProbeAgent.Settings { Temperature = Agent.Temperature.Updated };

        // Act
        var options = await Runtime.ResolveChatOptionsAsync(Agent.Id.Default, run, ct: Ct);

        // Assert
        options.Temperature.Should().Be(Agent.Temperature.Updated);
    }

    [Fact]
    public async Task Given_A_Stored_Value_Set_Back_To_Null_The_Layer_Below_Applies_Again()
    {
        // Arrange
        await Runtime.ConfigureAsync(Agent.Id.Default, settings => settings with
        {
            Probe = new ProbeToolset.ProbeOverrides { Limit = Data.Tool.Limit }
        }, Ct);

        await Runtime.ConfigureAsync(Agent.Id.Default, settings => settings with
        {
            Probe = settings.Probe with { Limit = null }
        }, Ct);

        var options = await Runtime.ResolveChatOptionsAsync(Agent.Id.Default, ct: Ct);

        // Act
        var settings = await ProbeAsync(options);

        // Assert
        settings.Limit.Should().Be(5);
    }
}
