using Typeshift.Tests.Approval.Support;
using FluentAssertions;

namespace Typeshift.Tests.Decisions;

public class StandingApprovalTests : ApprovalTestBase
{
    [Fact]
    public async Task Given_An_Approval_For_The_Conversation_A_Later_Call_Of_The_Tool_Runs_Without_Asking()
    {
        // Arrange
        await ApproveForTheConversationAsync();

        ChatClient
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var later = await RunAsync(Data.Agent.FollowUp);

        // Assert
        later.Decisions.Should().BeEmpty();
        ToolCalls.Should().Equal(
            $"close_ticket {Data.Agent.Ticket.Default}",
            $"close_ticket {Data.Agent.Ticket.Updated}");
    }

    [Fact]
    public async Task Given_An_Approval_For_The_Conversation_It_Is_Listed_With_The_Conversations_Decisions()
    {
        // Arrange
        await ApproveForTheConversationAsync();

        // Act
        var decisions = await DecisionsAsync();

        // Assert
        decisions.Standing.Should().Equal(new StandingApproval("close_ticket", null, Time.GetUtcNow()));
    }

    [Fact]
    public async Task Given_An_Approval_For_The_Conversation_Recorded_And_Not_Carried_Out_Nothing_Is_Granted_Yet()
    {
        // Arrange
        ChatClient.Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket));
        var asked = await RunAsync(Data.Tool.Question);

        // Act
        var recorded = await Chat.RecordAnswersAsync(
            [DecisionAnswer.ApproveForConversation(asked.Decisions[0].Id)],
            Ct);

        // Assert
        recorded.Standing.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_An_Approval_For_The_Conversation_It_Holds_Beyond_A_Summary()
    {
        // Arrange
        await ApproveForTheConversationAsync();

        ChatClient
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer))
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated))
            .Reply(Text(Data.Tool.Answer));

        await Chat.RunAsync(new string('x', Page), settings: new() { MaxInputTokens = 1_000 }, ct: Ct);
        var summarized = (await EntriesAsync(Data.Agent.ConversationId)).Any(entry => entry.IsSummary);

        // Act
        var later = await RunAsync(Data.Agent.FollowUp);

        // Assert
        summarized.Should().BeTrue();
        later.Decisions.Should().BeEmpty();
        ToolCalls.Should().EndWith($"close_ticket {Data.Agent.Ticket.Updated}");
    }

    [Fact]
    public async Task Given_A_Rejection_The_Tool_Is_Not_Approved_For_The_Conversation()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated));

        var asked = await RunAsync(Data.Tool.Question);
        await AnswerAsync(new DecisionAnswer(asked.Decisions[0].Id) { ForConversation = true });

        // Act
        var later = await RunAsync(Data.Agent.FollowUp);

        // Assert
        later.Decisions.Should().ContainSingle();
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Standing_Approval_Withdrawn_The_Next_Call_Of_The_Tool_Asks()
    {
        // Arrange
        await ApproveForTheConversationAsync();
        ChatClient.Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated));

        // Act
        await Chat.RevokeStandingApprovalAsync("close_ticket", ct: Ct);
        var later = await RunAsync(Data.Agent.FollowUp);

        // Assert
        later.Decisions.Should().ContainSingle();
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_A_Standing_Approval_Withdrawn_It_Is_No_Longer_Listed()
    {
        // Arrange
        await ApproveForTheConversationAsync();

        // Act
        await Chat.RevokeStandingApprovalAsync("close_ticket", ct: Ct);
        var decisions = await DecisionsAsync();

        // Assert
        decisions.Standing.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Standing_Approval_Withdrawing_Another_Tool_Keeps_It()
    {
        // Arrange
        await ApproveForTheConversationAsync();

        // Act
        await Chat.RevokeStandingApprovalAsync("note", ct: Ct);
        var decisions = await DecisionsAsync();

        // Assert
        decisions.Standing.Select(standing => standing.Tool).Should().Equal("close_ticket");
    }

    [Fact]
    public async Task Given_No_Standing_Approval_Withdrawing_One_Stores_Nothing()
    {
        // Act
        await Chat.RevokeStandingApprovalAsync("close_ticket", ct: Ct);

        // Assert
        (await StoredDecisionsAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Given_A_Standing_Approval_Withdrawn_And_Given_Again_The_Tool_Runs_Without_Asking()
    {
        // Arrange
        await ApproveForTheConversationAsync();
        await Chat.RevokeStandingApprovalAsync("close_ticket", ct: Ct);

        ChatClient
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated))
            .Reply(Text(Data.Tool.Answer))
            .Reply(CloseTicket($"{Data.Tool.SecondCallId}_later", Data.Agent.Ticket.Updated))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Agent.FollowUp);
        await AnswerAsync(asked.Decisions.OfType<ApprovalDecision>().Single().ApproveForConversation());

        // Act
        var later = await RunAsync(Data.Agent.Steering);

        // Assert
        later.Decisions.Should().BeEmpty();
        ToolCalls.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_A_Run_Going_On_Withdrawing_A_Standing_Approval_Leaves_It_The_Approvals_It_Began_With()
    {
        // Arrange
        await ApproveForTheConversationAsync();
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated))
            .Reply(Text(Data.Tool.Answer));

        var run = RunAsync(Data.Agent.FollowUp);
        await ChatClient.RequestedAsync(3);

        // Act
        await Chat.RevokeStandingApprovalAsync("close_ticket", ct: Ct);
        gate.SetResult();
        var response = await run;

        // Assert
        response.Decisions.Should().BeEmpty();
        ToolCalls.Should().EndWith($"close_ticket {Data.Agent.Ticket.Updated}");
    }

    [Fact]
    public async Task Given_A_Host_Without_A_Conversation_Withdrawing_Fails_Naming_WithConversationId()
    {
        // Act
        var revoke = () => Host<GuardedAgent>().RevokeStandingApprovalAsync("close_ticket", ct: Ct);

        // Assert
        await revoke.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WithConversationId*");
    }

    private async Task ApproveForTheConversationAsync()
    {
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);
        await AnswerAsync(asked.Decisions.OfType<ApprovalDecision>().Single().ApproveForConversation());
    }
}
