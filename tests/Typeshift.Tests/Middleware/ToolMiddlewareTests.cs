using Typeshift.Tests.Middleware.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Middleware;

public class ToolMiddlewareTests : MiddlewareTestBase
{
    [Fact]
    public async Task Given_Tool_Middleware_It_Wraps_Each_Call_The_First_Registered_Outermost()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "note", ("text", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        Script.Log.Where(entry => entry.StartsWith("tool")).Should().Equal(
            "tool 0 before note",
            "tool 1 before note",
            "tool 1 after note",
            "tool 0 after note");
        ToolCalls.Should().Equal($"note {Data.Tool.Query}");
    }

    [Fact]
    public async Task Given_Tool_Middleware_That_Answers_The_Tool_Does_Not_Run_And_The_Model_Receives_The_Answer()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "note", ("text", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        Script.Tools[0] = (_, _) => ValueTask.FromResult<object?>(Data.Agent.Reason);

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        ToolCalls.Should().BeEmpty();
        ChatClient.Requests[1]
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Select(result => result.Result?.ToString())
            .Should()
            .Equal(Data.Agent.Reason);
    }
}
