using Typeshift.Tests.Approval.Support;
using Typeshift.Tests.Decisions.Support;
using FluentAssertions;

namespace Typeshift.Tests.Decisions;

public class DecisionConcurrencyTests : DecisionTestBase
{
    [Fact]
    public async Task Given_Two_Processes_Approving_One_Decision_At_Once_The_Call_Runs_Once()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);
        var approval = DecisionAnswer.Approve(asked.Decisions[0].Id);
        var other = OtherProcess<GuardedAgent>();
        Store.BeforeNextSave(() => other.RecordAnswersAsync([approval], Ct));

        // Act
        await AnswerAsync(approval);

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_Two_Processes_Answering_One_Decision_Differently_At_Once_The_First_Answer_Stands()
    {
        // Arrange
        ChatClient.Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket));

        var asked = await RunAsync(Data.Tool.Question);
        var decision = asked.Decisions[0].Id;
        var other = OtherProcess<GuardedAgent>();
        Store.BeforeNextSave(() => other.RecordAnswersAsync([DecisionAnswer.Reject(decision)], Ct));

        // Act
        var answer = () => AnswerAsync(DecisionAnswer.Approve(decision));

        // Assert
        await answer.Should()
            .ThrowAsync<DecisionClosedException>()
            .Where(closed => closed.Decisions[decision] == DecisionClosedReason.Answered);
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Two_Processes_Continuing_One_Answered_Batch_One_Carries_It_Out_And_The_Other_Throws()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);
        var other = OtherProcess<GuardedAgent>();
        await Chat.RecordAnswersAsync([DecisionAnswer.Approve(asked.Decisions[0].Id)], Ct);
        Store.BeforeNextSave(() => other.ResumeAsync(cancellationToken: Ct));

        // Act
        var resume = () => Chat.ResumeAsync(cancellationToken: Ct);

        // Assert
        await resume.Should().ThrowAsync<DecisionClosedException>();
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_An_Answered_Batch_Another_Process_Carried_Out_And_Followed_With_A_New_One_The_First_Throws()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated));

        var other = OtherProcess<GuardedAgent>();
        var asked = await Chat.RunAsync(Data.Tool.Question, cancellationToken: Ct);
        await Chat.RecordAnswersAsync([DecisionAnswer.Approve(asked.Decisions[0].Id)], Ct);

        Store.BeforeNextSave(async () =>
        {
            await other.ResumeAsync(cancellationToken: Ct);
            var next = await other.RunAsync(Data.Agent.FollowUp, cancellationToken: Ct);
            await other.RecordAnswersAsync([DecisionAnswer.Approve(next.Decisions[0].Id)], Ct);
        });

        // Act
        var resume = () => Chat.ResumeAsync(cancellationToken: Ct);

        // Assert
        await resume.Should().ThrowAsync<DecisionClosedException>();
    }

    [Fact]
    public async Task Given_A_Started_Batch_Another_Process_Repairs_The_Process_Carrying_It_Out_Stops_As_Busy()
    {
        // Arrange
        ChatClient
            .Reply(SendRefund(Data.Tool.CallId))
            .Reply(Text(Data.Tool.Answer));

        var asked = await Held.RunAsync(Data.Tool.Question, cancellationToken: Ct);
        var carrying = Held.AnswerAsync([DecisionAnswer.Approve(asked.Decisions[0].Id)], cancellationToken: Ct);
        await Hold.Running.Task.WaitAsync(Ct);
        await OtherProcess<HeldAgent>().RunAsync(Data.Agent.FollowUp, cancellationToken: Ct);

        // Act
        Hold.Release.SetResult();

        // Assert
        await FluentActions.Awaiting(() => carrying).Should().ThrowAsync<ConversationBusyException>();
    }
}
