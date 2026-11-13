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
        Store.BeforeNextSave(() => other.ResumeAsync(ct: Ct));

        // Act
        var resume = () => Chat.ResumeAsync(ct: Ct);

        // Assert
        await resume.Should().ThrowAsync<DecisionClosedException>();
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }
}
