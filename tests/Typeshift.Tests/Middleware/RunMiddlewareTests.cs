using Typeshift.Tests.Middleware.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Middleware;

public class RunMiddlewareTests : MiddlewareTestBase
{
    [Fact]
    public async Task Given_Run_Middleware_It_Wraps_The_Run_The_First_Registered_Outermost()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        Script.Log.Should().Equal(
            "run 0 before",
            "run 1 before",
            $"run 1 after: {Data.Tool.Answer}",
            $"run 0 after: {Data.Tool.Answer}");
    }

    [Fact]
    public async Task Given_Run_Middleware_That_Changes_The_Messages_The_Model_Receives_Them()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        Script.Runs[0] = (run, next) =>
        {
            run.Messages.Add(new ChatMessage(ChatRole.User, Data.Agent.FollowUp));
            return next();
        };

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        ChatClient.Requests[0].Select(message => message.Text).Should().Equal(Data.Tool.Question, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_Run_Middleware_That_Adds_A_Message_The_Conversation_Does_Not_Store_It()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        Script.Runs[0] = (run, next) =>
        {
            run.Messages.Add(new ChatMessage(ChatRole.User, Data.Agent.FollowUp));
            return next();
        };

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        (await EntriesAsync(Data.Agent.ConversationId))
            .SelectMany(entry => entry.Messages)
            .Select(message => message.Text)
            .Should()
            .Equal(Data.Tool.Question, Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_Run_Middleware_That_Answers_The_Model_Is_Not_Called_And_The_Conversation_Stores_It()
    {
        // Arrange
        Script.Runs[1] = (run, _) =>
        {
            run.Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, Data.Tool.Answer));
            return Task.CompletedTask;
        };

        // Act
        var response = await RunAsync(Data.Tool.Question);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
        ChatClient.Requests.Should().BeEmpty();
        (await EntriesAsync(Data.Agent.ConversationId))
            .SelectMany(entry => entry.Messages)
            .Select(message => message.Text)
            .Should()
            .Equal(Data.Tool.Question, Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Streaming_Run_The_Middleware_Reads_The_Response_Once_The_Stream_Ended()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await StreamAsync(Data.Tool.Question);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
        Script.Log.Should().EndWith($"run 0 after: {Data.Tool.Answer}");
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Middleware_That_Answers_Is_Streamed()
    {
        // Arrange
        Script.Runs[0] = (run, _) =>
        {
            run.Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, Data.Tool.Answer));
            return Task.CompletedTask;
        };

        // Act
        var response = await StreamAsync(Data.Tool.Question);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
        ChatClient.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Left_Early_The_Run_Ends_With_What_Arrived()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer), Text(Data.Agent.FollowUp));

        var stream = Host<ContextAgent>().RunStreamingAsync(
            Data.Tool.Question,
            conversationId: Data.Agent.ConversationId,
            ct: Ct);

        // Act
        await foreach (var _ in stream)
        {
            break;
        }

        var entries = await EntriesAsync(Data.Agent.ConversationId);

        // Assert
        entries[1].Messages[0].Text.Should().StartWith(Data.Tool.Answer);
        entries[^1].EndsRun.Should().BeTrue();
    }

    [Fact]
    public async Task Given_Run_Middleware_That_Answers_While_Steered_The_Run_Ends_And_Stores_The_Input()
    {
        // Arrange
        Script.Runs[0] = (run, _) =>
        {
            Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
            run.Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, Data.Tool.Answer));
            return Task.CompletedTask;
        };

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        (await EntriesAsync(Data.Agent.ConversationId))
            .SelectMany(entry => entry.Messages)
            .Select(message => message.Text)
            .Should()
            .Equal(Data.Tool.Question, Data.Tool.Answer, Data.Agent.Steering);
    }

    [Fact]
    public async Task Given_Middleware_That_Prepends_A_Message_An_Approval_Still_Runs_The_Stored_Call()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);

        Script.Runs[0] = (run, next) =>
        {
            run.Messages.Insert(0, new ChatMessage(ChatRole.User, Data.Agent.FollowUp));
            return next();
        };

        var approval = new ChatMessage(ChatRole.User, [asked.ApprovalRequests[0].CreateResponse(approved: true)]);

        // Act
        await Host<ContextAgent>().RunAsync(approval, conversationId: Data.Agent.ConversationId, ct: Ct);

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_Run_Middleware_That_Neither_Calls_Next_Nor_Answers_The_Run_Fails()
    {
        // Arrange
        Script.Runs[0] = (_, _) => Task.CompletedTask;

        // Act
        var run = () => RunAsync(Data.Tool.Question);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*next*");
    }
}
