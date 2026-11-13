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

    [Fact]
    public async Task Given_Tool_Middleware_That_Ends_The_Tool_Loop_The_Result_Is_Stored()
    {
        // Arrange
        ChatClient.Reply(Call(Data.Tool.CallId, "note", ("text", Data.Tool.Query)));

        Script.Tools[0] = (call, next) =>
        {
            call.Terminate = true;
            return next();
        };

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        (await EntriesAsync(Data.Agent.ConversationId))
            .SelectMany(entry => entry.Messages)
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Should()
            .ContainSingle(result => result.CallId == Data.Tool.CallId);
    }

    [Fact]
    public async Task Given_A_Call_That_Waits_For_Approval_Tool_Middleware_Sees_It_Only_Once_It_Runs()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Text(Data.Tool.Answer));

        var chat = Host<ContextAgent>().WithConversationId(Data.Agent.ConversationId);
        var asked = await RunAsync(Data.Tool.Question);
        var seenWhileWaiting = Script.Log.Count(entry => entry.StartsWith("tool"));

        // Act
        await chat.AnswerAsync([DecisionAnswer.Approve(asked.Decisions[0].Id)], ct: Ct);

        // Assert
        seenWhileWaiting.Should().Be(0);
        Script.Log.Where(entry => entry.StartsWith("tool")).Should().StartWith("tool 0 before close_ticket");
    }

    [Fact]
    public async Task Given_A_Rejected_Call_Tool_Middleware_Never_Sees_It()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Text(Data.Tool.Answer));

        var chat = Host<ContextAgent>().WithConversationId(Data.Agent.ConversationId);
        var asked = await RunAsync(Data.Tool.Question);

        // Act
        await chat.AnswerAsync([asked.Decisions[0].Reject(Data.Agent.Reason)], ct: Ct);

        // Assert
        Script.Log.Where(entry => entry.StartsWith("tool")).Should().BeEmpty();
        ToolCalls.Should().BeEmpty();
    }
}
