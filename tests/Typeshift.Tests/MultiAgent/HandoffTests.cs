using Typeshift.Tests.MultiAgent.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.MultiAgent;

public class HandoffTests : MultiAgentTestBase
{
    [Fact]
    public async Task Given_A_Handoff_The_Agent_Handed_To_Answers_In_The_Same_Run_With_The_Whole_Conversation()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await RunAsync(Data.Tool.Question);

        // Assert
        response.Messages[^1].AuthorName.Should().Be("back_agent");
        response.Text.Should().EndWith(Data.Tool.Answer);
        ChatClient.Options[1]!.Instructions.Should().Be(BackAgent.Prompt);
        ChatClient.Requests[1][0].Text.Should().Be(Data.Tool.Question);
        ChatClient.Requests[1].SelectMany(message => message.Contents).OfType<FunctionResultContent>()
            .Should()
            .ContainSingle(result => result.CallId == Data.Tool.CallId);
    }

    [Fact]
    public async Task Given_A_Conversation_Handed_Off_Its_Next_Run_Goes_To_The_Agent_That_Answered()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason));

        await RunAsync(Data.Tool.Question);

        // Act
        var response = await RunAsync(Data.Agent.FollowUp);

        // Assert
        Models[^1].Model.Should().Be("back-model");
        response.Messages[^1].AuthorName.Should().Be("back_agent");
    }

    [Fact]
    public async Task Given_A_Handoff_To_An_Agent_It_Does_Not_Know_The_Run_Goes_On_And_Ends()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_nobody"))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await RunAsync(Data.Tool.Question);

        // Assert
        response.Text.Should().EndWith(Data.Tool.Answer);
        (await EntriesAsync(Data.Agent.ConversationId))[^1].EndsRun.Should().BeTrue();
    }

    [Fact]
    public async Task Given_No_Conversation_Handoffs_Are_Not_Offered()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<FrontAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ChatClient.Options[0]!.Tools.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Given_A_Streaming_Run_A_Handoff_Streams_The_Answer_Of_The_Agent_Handed_To()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var updates = await Host<FrontAgent>()
            .RunStreamingAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct)
            .ToListAsync(Ct);

        // Assert
        updates[^1].AuthorName.Should().Be("back_agent");
        updates.ToChatResponse().Text.Should().EndWith(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_Steering_Input_Waiting_At_A_Handoff_The_Agent_Handed_To_Answers_It()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer));

        var run = RunAsync(Data.Tool.Question);
        await ChatClient.Requested;

        // Act
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        gate.SetResult();
        var response = await run;

        // Assert
        response.Messages[^1].AuthorName.Should().Be("back_agent");
        ChatClient.Requests[1].Select(message => message.Text).Should().Contain(Data.Agent.Steering);
        ChatClient.Requests[1][^1].Text.Should().Be(Data.Agent.Steering);
    }

    private Task<ChatResponse> RunAsync(string input) =>
        Host<FrontAgent>().RunAsync(input, conversationId: Data.Agent.ConversationId, ct: Ct);
}
