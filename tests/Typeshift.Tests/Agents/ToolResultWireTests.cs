using System.Text.Json;
using Typeshift.Tests.Agents.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Agents;

public class ToolResultWireTests : AgentTestBase
{
    [Fact]
    public async Task Given_A_Tool_Result_Of_Json_The_Provider_Receives_It_As_Text()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, cancellationToken: Ct);

        // Assert
        Sent().Should().BeOfType<string>();
    }

    [Fact]
    public async Task Given_A_Tool_Result_Of_Json_The_Provider_Receives_It_Without_Line_Breaks()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, cancellationToken: Ct);

        // Assert
        $"{Sent()}".Should().NotContain("\n");
    }

    [Fact]
    public async Task Given_A_Tool_Result_Of_Json_The_Provider_Receives_Every_Member_Of_It()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ProbeAgent>().RunAsync(Data.Tool.Question, cancellationToken: Ct);
        var result = (JsonElement)response.Assert.ToolResult(Data.Tool.CallId).Result!;

        // Assert
        JsonElement.DeepEquals(JsonElement.Parse($"{Sent()}"), result).Should().BeTrue();
    }

    [Fact]
    public async Task Given_A_Tool_Result_Of_Json_The_Response_Still_Holds_It_As_Json()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ProbeAgent>().RunAsync(Data.Tool.Question, cancellationToken: Ct);

        // Assert
        response.Assert.ToolResult(Data.Tool.CallId).Result.Should().BeOfType<JsonElement>();
    }

    [Fact]
    public async Task Given_A_Streamed_Run_With_A_Tool_Result_Of_Json_The_Provider_Receives_It_As_Text()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunStreamingAsync(Data.Tool.Question, cancellationToken: Ct).ToListAsync(Ct);

        // Assert
        Sent().Should().BeOfType<string>();
    }

    private object? Sent() =>
        ChatClient.Requests[1]
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Single()
            .Result;
}
