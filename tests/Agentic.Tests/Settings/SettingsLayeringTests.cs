using System.Text.Json;
using Agentic.Tests.Agents.Support;
using Agentic.Tests.Toolsets.Companies;
using Agentic.Tests.Toolsets.Support;
using Agentic.Agents;
using Agentic.Settings;
using Agentic.Tools;
using FluentAssertions;

namespace Agentic.Tests.Settings;

public class SettingsLayeringTests : AgentTestBase
{
    [Fact]
    public async Task Given_Stored_Tool_Defaults_And_No_Code_Settings_The_Defaults_Apply()
    {
        // Arrange
        await ToolDefaults.ConfigureAsync(
            CompanyToolset.Tools.FindCompanies,
            settings => settings.Limit = Data.Tool.Limit,
            Ct);

        var options = await Runtime.ResolveChatOptionsAsync(ct: Ct);

        // Act
        var settings = await FindCompaniesAsync(options);

        // Assert
        settings.Limit.Should().Be(Data.Tool.Limit.Default);
    }

    [Fact]
    public async Task Given_Stored_Tool_Defaults_And_Code_Settings_Of_The_Agent_The_Defaults_Do_Not_Apply()
    {
        // Arrange
        await ToolDefaults.ConfigureAsync(ProbeToolset.Tools.Probe, settings => settings.Limit = Data.Tool.Limit, Ct);

        var options = await Runtime.ResolveChatOptionsAsync(ct: Ct);

        // Act
        var settings = await ProbeAsync(options);

        // Assert
        settings.Limit.Should().Be(5);
    }

    [Fact]
    public async Task Given_Stored_Agent_Overrides_They_Beat_The_Code_Settings_For_Allowlisted_Members_Only()
    {
        // Arrange
        var stored = new Dictionary<ToolName, StoredSettings>
        {
            [ProbeToolset.Tools.Probe.Name] = Stored(
                (nameof(ProbeSettings.Limit), Data.Tool.Limit.Default),
                (nameof(ProbeSettings.Mode), Data.Tool.Mode))
        };

        await Runtime.UpdateConfigurationAsync(configuration => configuration with { ToolSettings = stored }, Ct);
        var options = await Runtime.ResolveChatOptionsAsync(ct: Ct);

        // Act
        var settings = await ProbeAsync(options);

        // Assert
        settings.Should().Be(new ProbeSettings { Limit = Data.Tool.Limit.Default, Mode = "thorough" });
    }

    [Fact]
    public async Task Given_Run_Overrides_They_Beat_The_Stored_Overrides_For_Allowlisted_Members_Only()
    {
        // Arrange
        await Runtime.ConfigureAsync(configuration => configuration.Probe.Limit = Data.Tool.Limit.Default, Ct);

        var settings = new ProbeSettings
        {
            Limit = Data.Tool.Limit.Updated,
            Mode = Data.Tool.Mode
        };

        var run = new RunOverrides
        {
            ToolSettings = [settings]
        };

        var options = await Runtime.ResolveChatOptionsAsync(run, Ct);

        // Act
        var resolved = await ProbeAsync(options);

        // Assert
        resolved.Should().Be(new ProbeSettings { Limit = Data.Tool.Limit.Updated, Mode = "thorough" });
    }

    [Fact]
    public async Task Given_A_Stored_Value_Set_Back_To_Null_The_Layer_Below_Applies_Again()
    {
        // Arrange
        await Runtime.ConfigureAsync(configuration => configuration.Probe.Limit = Data.Tool.Limit.Default, Ct);
        await Runtime.ConfigureAsync(configuration => configuration.Probe.Limit = null, Ct);

        var options = await Runtime.ResolveChatOptionsAsync(ct: Ct);

        // Act
        var settings = await ProbeAsync(options);

        // Assert
        settings.Limit.Should().Be(5);
    }

    [Fact]
    public async Task Given_A_Stored_Json_Null_The_Layer_Below_Applies()
    {
        // Arrange
        var values = new Dictionary<string, JsonElement>
        {
            [nameof(ProbeSettings.Limit)] = JsonElement.Parse("null")
        };

        var stored = new Dictionary<ToolName, StoredSettings>
        {
            [ProbeToolset.Tools.Probe.Name] = new() { Values = values }
        };

        await Runtime.UpdateConfigurationAsync(configuration => configuration with { ToolSettings = stored }, Ct);
        var options = await Runtime.ResolveChatOptionsAsync(ct: Ct);

        // Act
        var settings = await ProbeAsync(options);

        // Assert
        settings.Limit.Should().Be(5);
    }
}
