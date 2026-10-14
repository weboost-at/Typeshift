using Typeshift.Tests.Approval.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Approval;

public class ModeTests : ApprovalTestBase
{
    [Fact]
    public async Task Given_Auto_Mode_A_Call_Needing_Approval_Runs_Without_Asking()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await RunAsync(Data.Tool.Question, AgentMode.Auto);

        // Assert
        response.ApprovalRequests.Should().BeEmpty();
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_Auto_Mode_The_Calls_It_Approves_Count_Toward_The_Runs_Limit()
    {
        // Arrange
        foreach (var call in Enumerable.Range(1, 4))
        {
            ChatClient.Reply(CloseTicket($"{Data.Tool.CallId}_{call}", Data.Agent.Ticket));
        }

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<GuardedAgent>().RunAsync(
            Data.Tool.Question,
            conversationId: Data.Agent.ConversationId,
            settings: new() { Mode = AgentMode.Auto, MaxToolIterations = 2 },
            ct: Ct);

        // Assert
        ChatClient.Requests.Should().HaveCount(3);
        ToolCalls.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_Auto_Mode_A_Rule_That_Rejects_Still_Wins()
    {
        // Arrange
        Rules.Decisions.Add(_ => ToolDecision.Reject(Data.Agent.Reason));

        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, AgentMode.Auto);

        // Assert
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Plan_Mode_Only_The_Read_Only_Tools_And_Exit_Plan_Mode_Are_Offered()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, AgentMode.Plan);

        // Assert
        ChatClient.Options[0]!.Tools!.Select(tool => tool.Name).Should().Equal("read_ticket", "exit_plan_mode");
    }

    [Fact]
    public async Task Given_Plan_Mode_The_Instructions_Ask_For_A_Plan()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, AgentMode.Plan);

        // Assert
        ChatClient.Options[0]!.Instructions.Should().Contain("exit_plan_mode");
    }

    [Fact]
    public async Task Given_Plan_Mode_Presenting_The_Plan_Ends_The_Run_With_A_Request_That_Carries_It()
    {
        // Arrange
        ChatClient.Reply(Call(Data.Tool.CallId, "exit_plan_mode", ("plan", Data.Tool.Answer)));

        // Act
        var response = await RunAsync(Data.Tool.Question, AgentMode.Plan);

        // Assert
        var call = (FunctionCallContent)response.ApprovalRequests.Single().ToolCall;
        call.Arguments!["plan"]!.ToString().Should().Contain(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_An_Approved_Plan_The_Run_Continues_In_The_Chosen_Mode()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "exit_plan_mode", ("plan", Data.Tool.Answer)))
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var planned = await RunAsync(Data.Tool.Question, AgentMode.Plan);

        // Act
        var response = await AnswerAsync(planned.ApprovalRequests[0].ApprovePlan(AgentMode.Auto));

        // Assert
        response.ApprovalRequests.Should().BeEmpty();
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
        ChatClient.Options[1]!.Tools!.Select(tool => tool.Name).Should().Contain("close_ticket");
    }

    [Fact]
    public async Task Given_A_Rejected_Plan_The_Model_Receives_The_Feedback_And_Stays_In_Plan_Mode()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "exit_plan_mode", ("plan", Data.Tool.Answer)))
            .Reply(Text(Data.Tool.Answer));

        var planned = await RunAsync(Data.Tool.Question, AgentMode.Plan);

        // Act
        await AnswerAsync(planned.ApprovalRequests[0].CreateResponse(approved: false, Data.Agent.Reason));

        // Assert
        ChatClient.Options[1]!.Tools!.Select(tool => tool.Name).Should().Equal("read_ticket", "exit_plan_mode");
        ResultSentToModel(1).Should().Contain(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Conversation_A_Run_Without_A_Mode_Continues_In_The_Mode_The_Last_Run_Ended_In()
    {
        // Arrange
        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(Data.Tool.Question, AgentMode.Plan);

        // Act
        await RunAsync(Data.Agent.FollowUp);

        // Assert
        ChatClient.Options[1]!.Tools!.Select(tool => tool.Name).Should().Equal("read_ticket", "exit_plan_mode");
    }

    [Fact]
    public async Task Given_A_Run_Naming_A_Mode_It_Leaves_The_Conversations_Mode()
    {
        // Arrange
        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(Data.Tool.Question, AgentMode.Plan);

        // Act
        await RunAsync(Data.Agent.FollowUp, AgentMode.Default);

        // Assert
        ChatClient.Options[1]!.Tools!.Select(tool => tool.Name).Should().Equal("close_ticket", "read_ticket", "note");
    }
}
