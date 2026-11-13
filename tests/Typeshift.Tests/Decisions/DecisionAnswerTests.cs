using Typeshift.Tests.Approval.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Decisions;

public class DecisionAnswerTests : ApprovalTestBase
{
    [Fact]
    public async Task Given_One_Of_Two_Decisions_Answered_No_Model_Is_Called_And_The_Response_Lists_The_Other()
    {
        // Arrange
        var asked = await AskTwiceAsync();

        // Act
        var response = await AnswerAsync(DecisionAnswer.Approve(asked.Decisions[0].Id));

        // Assert
        response.Decisions.Select(decision => decision.Id).Should().Equal(asked.Decisions[1].Id);
        ChatClient.Requests.Should().ContainSingle();
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_The_Last_Answer_The_Run_Continues_And_Carries_Every_Answer_Out()
    {
        // Arrange
        var asked = await AskTwiceAsync();
        ChatClient.Reply(Text(Data.Tool.Answer));
        await AnswerAsync(DecisionAnswer.Approve(asked.Decisions[0].Id));

        // Act
        var response = await AnswerAsync(asked.Decisions[1].Reject(Data.Agent.Reason));

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
        ResultsSentToModel(1).Should().Contain($"The user rejected this call. {Data.Agent.Reason}");
    }

    [Fact]
    public async Task Given_The_Same_Answer_Recorded_Twice_The_Call_Runs_Once()
    {
        // Arrange
        var asked = await AskAsync();
        var answer = DecisionAnswer.Approve(asked.Decisions[0].Id);
        ChatClient.Reply(Text(Data.Tool.Answer));
        await Chat.RecordAnswersAsync([answer], Ct);

        // Act
        await AnswerAsync(answer);

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_An_Answer_Repeated_After_The_Run_Continued_It_Throws_Naming_It_Answered_And_Runs_Nothing()
    {
        // Arrange
        var asked = await AskAsync();
        var answer = DecisionAnswer.Approve(asked.Decisions[0].Id);
        ChatClient.Reply(Text(Data.Tool.Answer));
        await AnswerAsync(answer);

        // Act
        var retried = () => AnswerAsync(answer);

        // Assert
        await retried.Should()
            .ThrowAsync<DecisionClosedException>()
            .Where(closed => closed.Decisions[answer.DecisionId] == DecisionClosedReason.Answered);
        ToolCalls.Should().ContainSingle();
        ChatClient.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_An_Answer_To_A_Decision_The_Conversation_Never_Asked_It_Throws_And_Calls_No_Model()
    {
        // Arrange
        await AskAsync();

        // Act
        var answer = () => AnswerAsync(DecisionAnswer.Approve(Data.Decisions.DecisionId));

        // Assert
        await answer.Should()
            .ThrowAsync<DecisionClosedException>()
            .Where(closed => closed.Decisions[Data.Decisions.DecisionId] == DecisionClosedReason.Unknown);
        ChatClient.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_Recorded_Answers_The_Decisions_Can_Resume_And_Resuming_Carries_Them_Out()
    {
        // Arrange
        var asked = await AskAsync();
        ChatClient.Reply(Text(Data.Tool.Answer));
        var recorded = await Chat.RecordAnswersAsync([DecisionAnswer.Approve(asked.Decisions[0].Id)], Ct);

        // Act
        var response = await Chat.ResumeAsync(ct: Ct);

        // Assert
        recorded.CanResume.Should().BeTrue();
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_Open_Decisions_Resuming_Calls_No_Model_And_Returns_Them()
    {
        // Arrange
        var asked = await AskAsync();

        // Act
        var response = await Chat.ResumeAsync(ct: Ct);

        // Assert
        response.Decisions.Select(decision => decision.Id).Should().Equal(asked.Decisions[0].Id);
        ChatClient.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_Open_Decisions_Resuming_A_Streaming_Run_Sends_Them_In_One_Update()
    {
        // Arrange
        var asked = await AskAsync();

        // Act
        var updates = await Chat.ResumeStreamingAsync(ct: Ct).ToListAsync(Ct);

        // Assert
        updates.Should().ContainSingle();
        updates[0].Decisions.Select(decision => decision.Id).Should().Equal(asked.Decisions[0].Id);
    }

    [Fact]
    public async Task Given_Open_Decisions_Loading_Them_Lists_Each_With_Its_Tool()
    {
        // Arrange
        var asked = await AskTwiceAsync();

        // Act
        var decisions = await DecisionsAsync();

        // Assert
        decisions.Open.Select(decision => decision.Id).Should().Equal(asked.Decisions.Select(decision => decision.Id));
        decisions.CanResume.Should().BeFalse();
    }

    [Fact]
    public async Task Given_A_Conversation_That_Never_Asked_Its_Decisions_Are_Empty()
    {
        // Act
        var decisions = await DecisionsAsync();

        // Assert
        decisions.Open.Should().BeEmpty();
        decisions.Standing.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Host_Without_A_Conversation_Loading_Decisions_Fails_Naming_WithConversationId()
    {
        // Act
        var load = () => Host<GuardedAgent>().GetDecisionsAsync(Ct);

        // Assert
        await load.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WithConversationId*");
    }

    [Fact]
    public async Task Given_A_Host_Without_A_Conversation_Answering_Fails_Naming_WithConversationId()
    {
        // Act
        var answer = () => Host<GuardedAgent>().AnswerAsync(
            [DecisionAnswer.Approve(Data.Decisions.DecisionId)],
            ct: Ct);

        // Assert
        await answer.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WithConversationId*");
    }

    [Fact]
    public async Task Given_The_Raw_Path_Recorded_Answers_Are_Carried_Out_By_A_Call_Without_Messages()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var chat = Chat;
        var options = await chat.ResolveOptionsAsync(ct: Ct);
        using var client = await chat.CreateChatClientAsync(options, Ct);
        var asked = await client.GetResponseAsync([new(ChatRole.User, Data.Tool.Question)], options, Ct);
        await chat.RecordAnswersAsync([DecisionAnswer.Approve(asked.Decisions[0].Id)], Ct);

        // Act
        var response = await client.GetResponseAsync([], options, Ct);

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
        response.Text.Should().Be(Data.Tool.Answer);
    }

    private async Task<ChatResponse> AskAsync()
    {
        ChatClient.Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket));
        return await RunAsync(Data.Tool.Question);
    }

    private async Task<ChatResponse> AskTwiceAsync()
    {
        ChatClient.Reply(Calls(
            CloseTicket(Data.Tool.CallId, Data.Agent.Ticket),
            CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated)));

        return await RunAsync(Data.Tool.Question);
    }
}
