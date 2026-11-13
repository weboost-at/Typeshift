using Typeshift.Tests.Approval.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Decisions;

public class ExpiredDecisionTests : ApprovalTestBase
{
    [Fact]
    public async Task Given_A_New_Message_In_Place_Of_An_Answer_The_Model_Reads_For_Each_Call_That_It_Did_Not_Run()
    {
        // Arrange
        ChatClient
            .Reply(Calls(
                CloseTicket(Data.Tool.CallId, Data.Agent.Ticket),
                Note(Data.Tool.SecondCallId, Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(Data.Tool.Question);

        // Act
        await RunAsync(Data.Agent.FollowUp);

        // Assert
        ResultsSentToModel(1).Should().Equal(DecisionResults.Expired, DecisionResults.Expired);
        ChatClient.Requests[1][^1].Text.Should().Be(Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_A_New_Message_In_Place_Of_An_Answer_The_Call_Next_To_The_Decision_Does_Not_Run()
    {
        // Arrange
        ChatClient
            .Reply(Calls(
                CloseTicket(Data.Tool.CallId, Data.Agent.Ticket),
                Note(Data.Tool.SecondCallId, Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(Data.Tool.Question);

        // Act
        await RunAsync(Data.Agent.FollowUp);

        // Assert
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_New_Message_In_Place_Of_An_Answer_A_Call_A_Rule_Approved_Beside_It_Does_Not_Run()
    {
        // Arrange
        Rules.Verdicts.Add(call => call.Arguments!["number"]!.ToString() == Data.Agent.Ticket.Default.ToString()
            ? RuleVerdict.Approve
            : RuleVerdict.Abstain);

        ChatClient
            .Reply(Calls(
                CloseTicket(Data.Tool.CallId, Data.Agent.Ticket),
                CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated)))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(Data.Tool.Question);

        // Act
        await RunAsync(Data.Agent.FollowUp);

        // Assert
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Decision_A_New_Message_Passed_By_An_Answer_To_It_Throws_Naming_It_Expired()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);
        var decision = asked.Decisions[0].Id;
        await RunAsync(Data.Agent.FollowUp);

        // Act
        var late = () => AnswerAsync(DecisionAnswer.Approve(decision));

        // Assert
        await late.Should()
            .ThrowAsync<DecisionClosedException>()
            .Where(closed => closed.Decisions[decision] == DecisionClosedReason.Expired);
        ToolCalls.Should().BeEmpty();
        ChatClient.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_An_Approval_For_The_Conversation_Recorded_Before_A_New_Message_It_Grants_Nothing()
    {
        // Arrange
        ChatClient
            .Reply(Calls(
                CloseTicket(Data.Tool.CallId, Data.Agent.Ticket),
                CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated)))
            .Reply(CloseTicket($"{Data.Tool.CallId}_later", Data.Agent.Ticket));

        var asked = await RunAsync(Data.Tool.Question);
        await Chat.RecordAnswersAsync([DecisionAnswer.ApproveForConversation(asked.Decisions[0].Id)], Ct);

        // Act
        var later = await RunAsync(Data.Agent.FollowUp);

        // Assert
        later.Decisions.Should().ContainSingle();
        (await DecisionsAsync()).Standing.Should().BeEmpty();
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Steering_After_The_Run_Asked_The_Model_Reads_That_The_Call_Did_Not_Run_And_Answers_It()
    {
        // Act
        var response = await SteerPastADecisionAsync(Text(Data.Tool.Answer));

        // Assert
        response.Decisions.Should().BeEmpty();
        response.Text.Should().Be(Data.Tool.Answer);
        ResultSentToModel(1).Should().Be(DecisionResults.Expired);
        ChatClient.Requests[1][^1].Text.Should().Be(Data.Agent.Steering);
    }

    [Fact]
    public async Task Given_Steering_After_The_Run_Asked_An_Answer_To_The_Decision_Throws_Naming_It_Expired()
    {
        // Arrange
        await SteerPastADecisionAsync(Text(Data.Tool.Answer));
        var decision = DecisionDocument.Deserialize((await StoredDecisionsAsync())!.Json).Closed.Single().Id;

        // Act
        var late = () => AnswerAsync(DecisionAnswer.Approve(decision));

        // Assert
        await late.Should()
            .ThrowAsync<DecisionClosedException>()
            .Where(closed => closed.Decisions[decision] == DecisionClosedReason.Expired);
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Steering_The_Model_Answers_By_Asking_Again_Only_The_New_Decision_Is_Returned_And_Runs()
    {
        // Arrange
        var asked = await SteerPastADecisionAsync(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated));
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await AnswerAsync(DecisionAnswer.Approve(asked.Decisions.Single().Id));

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Updated}");
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Steered_After_It_Asked_Its_Updates_Carry_No_Decision()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var run = StreamAsync(Data.Tool.Question);
        await ChatClient.Requested.WaitAsync(Ct);
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);

        // Act
        gate.SetResult();
        var response = await run;

        // Assert
        response.Decisions.Should().BeEmpty();
        response.Text.Should().Be(Data.Tool.Answer);
        ToolCalls.Should().BeEmpty();
    }

    // A run steered while its model call is under way: the call that needs approval comes back, the batch opens, and
    // the steering input passes it by before the run ends
    private async Task<ChatResponse> SteerPastADecisionAsync(ChatResponseUpdate next)
    {
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(next);

        var run = RunAsync(Data.Tool.Question);
        await ChatClient.Requested.WaitAsync(Ct);
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        gate.SetResult();

        return await run;
    }
}
