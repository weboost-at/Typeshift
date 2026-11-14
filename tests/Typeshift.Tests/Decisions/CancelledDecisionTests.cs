using Typeshift.Tests.Approval.Support;
using Typeshift.Tests.Decisions.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Decisions;

public class CancelledDecisionTests : DecisionTestBase
{
    [Fact]
    public async Task Given_Open_Decisions_Cancelling_Them_Leaves_Nothing_For_Your_App_To_Answer()
    {
        // Arrange
        await AskBesideANoteAsync();

        // Act
        var cancelled = await Chat.CancelDecisionsAsync(Ct);

        // Assert
        cancelled.Open.Should().BeEmpty();
        cancelled.CanResume.Should().BeFalse();
        (await DecisionsAsync()).Open.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Cancelled_Decisions_The_Next_Run_Tells_The_Model_That_No_Call_Of_Them_Ran()
    {
        // Arrange
        await AskBesideANoteAsync();
        ChatClient.Reply(Text(Data.Tool.Answer));
        await Chat.CancelDecisionsAsync(Ct);

        // Act
        await RunAsync(Data.Agent.FollowUp);

        // Assert
        ResultsSentToModel(1).Should().Equal(DecisionResults.Cancelled, DecisionResults.Cancelled);
        ChatClient.Requests[1][^1].Text.Should().Be(Data.Agent.FollowUp);
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Cancelled_Decisions_A_Streaming_Run_Tells_The_Model_That_No_Call_Of_Them_Ran()
    {
        // Arrange
        await AskBesideANoteAsync();
        ChatClient.Reply(Text(Data.Tool.Answer));
        await Chat.CancelDecisionsAsync(Ct);

        // Act
        var response = await StreamAsync(Data.Agent.FollowUp);

        // Assert
        ResultsSentToModel(1).Should().Equal(DecisionResults.Cancelled, DecisionResults.Cancelled);
        response.Text.Should().Be(Data.Tool.Answer);
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Cancelled_Decisions_Resuming_Lets_The_Model_Go_On_Without_Input()
    {
        // Arrange
        await AskBesideANoteAsync();
        ChatClient.Reply(Text(Data.Tool.Answer));
        await Chat.CancelDecisionsAsync(Ct);

        // Act
        var resumed = await Chat.ResumeAsync(ct: Ct);

        // Assert
        resumed.Text.Should().Be(Data.Tool.Answer);
        ResultsSentToModel(1).Should().Equal(DecisionResults.Cancelled, DecisionResults.Cancelled);
    }

    [Fact]
    public async Task Given_Cancelled_Decisions_An_Answer_Throws_Naming_Them_Cancelled_And_Calls_No_Model()
    {
        // Arrange
        var asked = await AskBesideANoteAsync();
        var decision = asked.Decisions.Single().Id;
        await Chat.CancelDecisionsAsync(Ct);

        // Act
        var late = () => AnswerAsync(DecisionAnswer.Approve(decision));

        // Assert
        await late.Should()
            .ThrowAsync<DecisionClosedException>()
            .Where(closed => closed.Decisions[decision] == DecisionClosedReason.Cancelled);
        ChatClient.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_Cancelled_Decisions_The_Next_Run_Passed_An_Answer_Still_Throws_Naming_Them_Cancelled()
    {
        // Arrange
        var asked = await AskBesideANoteAsync();
        var decision = asked.Decisions.Single().Id;
        ChatClient.Reply(Text(Data.Tool.Answer));
        await Chat.CancelDecisionsAsync(Ct);
        await RunAsync(Data.Agent.FollowUp);

        // Act
        var late = () => AnswerAsync(DecisionAnswer.Approve(decision));

        // Assert
        await late.Should()
            .ThrowAsync<DecisionClosedException>()
            .Where(closed => closed.Decisions[decision] == DecisionClosedReason.Cancelled);
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_An_Approval_Recorded_And_Not_Carried_Out_Cancelling_Withdraws_It()
    {
        // Arrange
        var asked = await AskBesideANoteAsync();
        ChatClient.Reply(Text(Data.Tool.Answer));
        await Chat.RecordAnswersAsync([DecisionAnswer.ApproveForConversation(asked.Decisions.Single().Id)], Ct);

        // Act
        var cancelled = await Chat.CancelDecisionsAsync(Ct);
        await RunAsync(Data.Agent.FollowUp);

        // Assert
        cancelled.CanResume.Should().BeFalse();
        ToolCalls.Should().BeEmpty();
        (await DecisionsAsync()).Standing.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Nothing_That_Waits_Cancelling_Stores_Nothing()
    {
        // Act
        var cancelled = await Chat.CancelDecisionsAsync(Ct);

        // Assert
        cancelled.Open.Should().BeEmpty();
        Store.Saves.Should().Be(0);
    }

    [Fact]
    public async Task Given_Answers_A_Run_Carried_Out_Cancelling_Changes_Nothing()
    {
        // Arrange
        var asked = await AskBesideANoteAsync();
        ChatClient.Reply(Text(Data.Tool.Answer));
        await AnswerAsync(DecisionAnswer.Approve(asked.Decisions.Single().Id));
        var saves = Store.Saves;

        // Act
        await Chat.CancelDecisionsAsync(Ct);

        // Assert
        Store.Saves.Should().Be(saves);
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}", $"note {Data.Tool.Query}");
    }

    [Fact]
    public async Task Given_Decisions_Cancelled_While_Their_Run_Takes_Steering_The_Run_Tells_The_Model_And_Answers()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        Store.BeforeNextSave(() => Task.CompletedTask);
        Store.BeforeNextSave(() => OtherProcess<GuardedAgent>().CancelDecisionsAsync(Ct));

        var run = RunAsync(Data.Tool.Question);
        await ChatClient.Requested.WaitAsync(Ct);
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);

        // Act
        gate.SetResult();
        var response = await run;

        // Assert
        response.Decisions.Should().BeEmpty();
        response.Text.Should().Be(Data.Tool.Answer);
        ResultSentToModel(1).Should().Be(DecisionResults.Cancelled);
    }

    [Fact]
    public async Task Given_A_Host_Without_A_Conversation_Cancelling_Fails_Naming_WithConversationId()
    {
        // Act
        var cancel = () => Host<GuardedAgent>().CancelDecisionsAsync(Ct);

        // Assert
        await cancel.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WithConversationId*");
    }

    // A run that asks for close_ticket beside a note, which waits with it
    private async Task<ChatResponse> AskBesideANoteAsync()
    {
        ChatClient.Reply(Calls(
            CloseTicket(Data.Tool.CallId, Data.Agent.Ticket),
            Note(Data.Tool.SecondCallId, Data.Tool.Query)));

        return await RunAsync(Data.Tool.Question);
    }
}
