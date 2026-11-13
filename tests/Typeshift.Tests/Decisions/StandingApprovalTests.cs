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

    private async Task ApproveForTheConversationAsync()
    {
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);
        await AnswerAsync(asked.Decisions.OfType<ApprovalDecision>().Single().ApproveForConversation());
    }
}
