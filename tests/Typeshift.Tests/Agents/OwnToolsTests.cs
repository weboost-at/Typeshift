using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Tools.Support;
using FluentAssertions;

namespace Typeshift.Tests.Agents;

public class OwnToolsTests : AgentTestBase
{
    [Fact]
    public async Task Given_A_Tool_Of_The_Agents_Own_The_Model_Is_Offered_It_After_The_Toolsets_Tools()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<OwnToolsAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ChatClient.Options[0]!.Tools!.Select(tool => tool.Name).Should().Equal("probe", "recall");
    }

    [Fact]
    public async Task Given_A_Tool_Of_The_Agents_Own_It_Runs_With_The_Agents_Settings_And_The_Runs()
    {
        // Arrange
        var settings = new OwnToolsAgent.Settings { Recall = new() { Limit = Data.Tool.Limit } };

        ChatClient
            .Reply(Call(Data.Tool.CallId, "recall", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<OwnToolsAgent>().RunAsync(Data.Tool.Question, settings: settings, ct: Ct);
        var result = response.Assert.ToolResult<ProbeResult>(Data.Tool.CallId, Json);

        // Assert
        result.Settings.Should().Be(new ProbeSettings { Limit = Data.Tool.Limit, Verbose = true });
    }
}
