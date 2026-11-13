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
        response.Decisions.Should().BeEmpty();
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
        await Host<GuardedAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(
            Data.Tool.Question,
            settings: new() { Mode = AgentMode.Auto, MaxToolIterations = 2 },
            ct: Ct);

        // Assert
        ChatClient.Requests.Should().HaveCount(3);
        ToolCalls.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_Steering_Input_Near_The_Runs_Limit_The_Later_Pass_Gets_What_Is_Left()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(Note(Data.Tool.CallId, Data.Tool.Query))
            .Reply(gate, Text(Data.Tool.Answer))
            .Reply(Note(Data.Tool.SecondCallId, Data.Tool.Query))
            .Reply(Note($"{Data.Tool.SecondCallId}_left", Data.Tool.Query))
            .Reply(Text(Data.Tool.Answer));

        var run = Chat.RunAsync(Data.Tool.Question, settings: new() { MaxToolIterations = 2 }, ct: Ct);
        await ChatClient.RequestedAsync(2).WaitAsync(Ct);
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);

        // Act
        gate.SetResult();
        await run;

        // Assert
        ChatClient.Requests.Should().HaveCount(4);
        ToolCalls.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_Auto_Mode_A_Rule_That_Rejects_Still_Wins()
    {
        // Arrange
        Rules.Verdicts.Add(_ => RuleVerdict.Reject(Data.Agent.Reason));

        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, AgentMode.Auto);

        // Assert
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Auto_Mode_A_Rule_That_Rejects_Without_A_Reason_The_Run_Fails_And_The_Call_Does_Not_Run()
    {
        // Arrange
        Rules.Verdicts.Add(_ => RuleVerdict.Reject(null!));

        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var run = () => RunAsync(Data.Tool.Question, AgentMode.Auto);

        // Assert
        await run.Should().ThrowAsync<ArgumentException>();
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
    public async Task Given_Plan_Mode_Presenting_The_Plan_Ends_The_Run_With_A_Decision_That_Carries_It()
    {
        // Arrange
        ChatClient.Reply(Call(Data.Tool.CallId, "exit_plan_mode", ("plan", Data.Tool.Answer)));

        // Act
        var response = await RunAsync(Data.Tool.Question, AgentMode.Plan);

        // Assert
        response.Decisions.OfType<PlanDecision>().Single().Plan.Should().Be(Data.Tool.Answer);
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
        var response = await AnswerAsync(planned.Decisions.OfType<PlanDecision>().Single().Approve(AgentMode.Auto));

        // Assert
        response.Decisions.Should().BeEmpty();
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
        ChatClient.Options[1]!.Tools!.Select(tool => tool.Name).Should().Contain("close_ticket");
        ResultSentToModel(1).Should().Be("The user approved the plan. Carry it out now.");
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
        await AnswerAsync(planned.Decisions[0].Reject(Data.Agent.Reason));

        // Assert
        ChatClient.Options[1]!.Tools!.Select(tool => tool.Name).Should().Equal("read_ticket", "exit_plan_mode");
        ResultSentToModel(1).Should().Be($"The user rejected the plan. {Data.Agent.Reason}");
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
    public async Task Given_A_Compaction_In_Plan_Mode_Its_Summary_Keeps_The_Mode()
    {
        // Arrange
        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(new string('x', Page), AgentMode.Plan);

        // Act
        await Host<GuardedAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(
            Data.Agent.FollowUp,
            settings: new() { Mode = AgentMode.Plan, MaxInputTokens = 1_000 },
            ct: Ct);

        var summary = (await EntriesAsync(Data.Agent.ConversationId)).Single(entry => entry.IsSummary);

        // Assert
        summary.Mode.Should().Be(AgentMode.Plan);
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

    [Fact]
    public async Task Given_Run_Settings_That_Name_No_Mode_The_Conversation_Keeps_Its_Mode()
    {
        // Arrange
        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(Data.Tool.Question, AgentMode.Plan);

        // Act
        await Chat.RunAsync(Data.Agent.FollowUp, settings: new() { MaxToolIterations = 5 }, ct: Ct);

        // Assert
        ChatClient.Options[1]!.Tools!.Select(tool => tool.Name).Should().Equal("read_ticket", "exit_plan_mode");
    }

    [Fact]
    public async Task Given_An_Approved_Plan_A_Later_Run_Naming_Plan_Mode_Plans_Again()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "exit_plan_mode", ("plan", Data.Tool.Answer)))
            .Reply(Text(Data.Tool.Answer))
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var planned = await RunAsync(Data.Tool.Question, AgentMode.Plan);
        await AnswerAsync(planned.Decisions.OfType<PlanDecision>().Single().Approve(AgentMode.Auto));

        // Act
        await RunAsync(Data.Agent.FollowUp, AgentMode.Plan);

        // Assert
        ChatClient.Options[2]!.Tools!.Select(tool => tool.Name).Should().Equal("read_ticket", "exit_plan_mode");
        ToolCalls.Should().BeEmpty();
    }

    [Theory]
    [InlineData(AgentMode.Default)]
    [InlineData(AgentMode.Auto)]
    public async Task Given_A_Run_That_Is_Not_Planning_A_Call_Of_Exit_Plan_Mode_Leaves_Your_App_Nothing_To_Decide(
        AgentMode mode)
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "exit_plan_mode", ("plan", Data.Tool.Answer)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await RunAsync(Data.Tool.Question, mode);

        // Assert
        response.Decisions.Should().BeEmpty();
        ResultSentToModel(1).Should().Be("You are not in plan mode: go on with the task.");
    }

    [Fact]
    public async Task Given_Auto_Mode_Without_A_Conversation_A_Call_Of_Exit_Plan_Mode_Does_Not_Fail_The_Run()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "exit_plan_mode", ("plan", Data.Tool.Answer)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<GuardedAgent>().RunAsync(
            Data.Tool.Question,
            settings: new() { Mode = AgentMode.Auto },
            ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }
}
