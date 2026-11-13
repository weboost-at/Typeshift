using Typeshift.Tests.Approval.Support;
using Typeshift.Tests.Decisions.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Decisions;

public class DecisionRecoveryTests : DecisionTestBase
{
    [Fact]
    public async Task Given_A_Store_That_Fails_To_Save_The_Batch_The_Run_Throws_And_Nothing_Waits()
    {
        // Arrange
        ChatClient.Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket));
        Store.FailingSave = 1;

        // Act
        var run = () => RunAsync(Data.Tool.Question);

        // Assert
        await run.Should().ThrowAsync<IOException>();
        (await DecisionsAsync()).Open.Should().BeEmpty();
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Batch_That_Was_Never_Saved_The_Next_Run_Leaves_Its_Calls_Out()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        Store.FailingSave = 1;
        await Record.ExceptionAsync(() => RunAsync(Data.Tool.Question));

        // Act
        await RunAsync(Data.Agent.FollowUp);
        var sent = ChatClient.Requests[1].SelectMany(message => message.Contents);

        // Assert
        sent.Should().NotContain(content => content is FunctionCallContent || content is FunctionResultContent);
    }

    [Fact]
    public async Task Given_A_Store_That_Failed_To_Append_The_Response_That_Asks_The_Run_Throws_The_Stores_Exception()
    {
        // Arrange
        ChatClient.Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket));
        Store.FailingAppend = 2;

        // Act
        var run = () => RunAsync(Data.Tool.Question);

        // Assert
        await run.Should().ThrowAsync<IOException>();
        (await DecisionsAsync()).Open.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Run_Whose_End_Was_Lost_After_It_Asked_Resuming_Stores_The_End_And_Returns_The_Decisions()
    {
        // Arrange
        ChatClient.Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket));
        Store.FailingAppend = 3;
        var asked = await RunAsync(Data.Tool.Question);

        // Act
        var resumed = await Chat.ResumeAsync(ct: Ct);
        var entries = await EntriesAsync(Data.Agent.ConversationId);

        // Assert
        resumed.Decisions.Select(decision => decision.Id).Should().Equal(asked.Decisions[0].Id);
        entries[^1].EndsRun.Should().BeTrue();
        ChatClient.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Run_Cancelled_While_Its_Approved_Call_Ran_The_Next_Run_Reads_Unknown_And_Repeats_Nothing()
    {
        // Arrange
        await StopWhileTheApprovedCallRunsAsync(SendRefund(Data.Tool.CallId));
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Held.RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        ToolCalls.Should().Equal($"send_refund {Data.Agent.Ticket.Default}");
        LastResults()[Data.Tool.CallId].Should().Be(DecisionResults.Unknown);
    }

    [Fact]
    public async Task Given_A_Run_Cancelled_While_Its_Approved_Call_Ran_Resuming_Reads_Unknown_And_Repeats_Nothing()
    {
        // Arrange
        await StopWhileTheApprovedCallRunsAsync(SendRefund(Data.Tool.CallId));
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Held.ResumeAsync(ct: Ct);

        // Assert
        ToolCalls.Should().Equal($"send_refund {Data.Agent.Ticket.Default}");
        LastResults()[Data.Tool.CallId].Should().Be(DecisionResults.Unknown);
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Cancelled_While_Its_Approved_Call_Ran_The_Next_Run_Repeats_Nothing()
    {
        // Arrange
        await StopWhileTheApprovedCallRunsAsync(SendRefund(Data.Tool.CallId), streaming: true);
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Held.RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        ToolCalls.Should().Equal($"send_refund {Data.Agent.Ticket.Default}");
        LastResults()[Data.Tool.CallId].Should().Be(DecisionResults.Unknown);
    }

    [Fact]
    public async Task Given_A_Process_Stopped_In_An_Approved_Call_Resuming_In_Another_Process_Repeats_Nothing()
    {
        // Arrange
        await StopWhileTheApprovedCallRunsAsync(SendRefund(Data.Tool.CallId));
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await OtherProcess<HeldAgent>().ResumeAsync(ct: Ct);

        // Assert
        ToolCalls.Should().Equal($"send_refund {Data.Agent.Ticket.Default}");
        LastResults()[Data.Tool.CallId].Should().Be(DecisionResults.Unknown);
    }

    [Fact]
    public async Task Given_Two_Approved_Calls_And_A_Run_Cancelled_During_The_Second_The_Next_Run_Repeats_Neither()
    {
        // Arrange
        await StopWhileTheApprovedCallRunsAsync(Calls(
            CloseTicket(Data.Tool.CallId, Data.Agent.Ticket),
            SendRefund(Data.Tool.SecondCallId)));

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Held.RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        ToolCalls.Should().Equal(
            $"close_ticket {Data.Agent.Ticket.Default}",
            $"send_refund {Data.Agent.Ticket.Default}");
        LastResults().Values.Should().OnlyContain(result => result == DecisionResults.Unknown);
    }

    [Fact]
    public async Task Given_A_Stopped_Run_A_Late_Answer_To_Its_Decision_Throws_Naming_It_Answered()
    {
        // Arrange
        var decision = await StopWhileTheApprovedCallRunsAsync(SendRefund(Data.Tool.CallId));

        // Act
        var late = () => Held.RecordAnswersAsync([DecisionAnswer.Approve(decision)], Ct);

        // Assert
        await late.Should()
            .ThrowAsync<DecisionClosedException>()
            .Where(closed => closed.Decisions[decision] == DecisionClosedReason.Answered);
    }

    [Fact]
    public async Task Given_A_Store_That_Failed_To_Append_The_Results_The_Next_Run_Reads_Unknown_And_Repeats_Nothing()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);
        Store.FailingAppend = 4;
        await AnswerAsync(DecisionAnswer.Approve(asked.Decisions[0].Id));

        // Act
        await RunAsync(Data.Agent.FollowUp);

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
        LastResults()[Data.Tool.CallId].Should().Be(DecisionResults.Unknown);
    }

    [Fact]
    public async Task Given_Results_Stored_And_The_Close_Lost_The_Next_Run_Keeps_The_Results_And_Only_Closes()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);
        Store.FailingSave = 4;
        await AnswerAsync(DecisionAnswer.Approve(asked.Decisions[0].Id));

        // Act
        await RunAsync(Data.Agent.FollowUp);
        var late = await Record.ExceptionAsync(() => AnswerAsync(DecisionAnswer.Approve(asked.Decisions[0].Id)));

        // Assert
        LastResults()[Data.Tool.CallId].Should().Contain($"closed {Data.Agent.Ticket.Default}");
        late.Should().BeOfType<DecisionClosedException>();
        ToolCalls.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Store_That_Failed_To_Append_What_Expired_The_Next_Run_Stores_It_Once_And_Closes()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(Data.Tool.Question);
        Store.FailingAppend = 4;
        await RunAsync(Data.Agent.FollowUp);

        // Act
        await RunAsync(Data.Agent.Steering);
        var entries = await EntriesAsync(Data.Agent.ConversationId);

        // Assert
        entries.SelectMany(entry => entry.Messages)
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Select(result => result.Result?.ToString())
            .Should()
            .Equal(DecisionResults.Expired);
        LastResults()[Data.Tool.CallId].Should().Be(DecisionResults.Expired);
    }

    private async Task<string> StopWhileTheApprovedCallRunsAsync(ChatResponseUpdate calls, bool streaming = false)
    {
        ChatClient.Reply(calls);

        var asked = await Held.RunAsync(Data.Tool.Question, ct: Ct);
        var answers = asked.Decisions.Select(decision => DecisionAnswer.Approve(decision.Id)).ToList();
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        Task answering = streaming
            ? Held.AnswerStreamingAsync(answers, ct: stopping.Token).ToListAsync(stopping.Token).AsTask()
            : Held.AnswerAsync(answers, ct: stopping.Token);

        await Hold.Running.Task.WaitAsync(Ct);
        await stopping.CancelAsync();
        await Record.ExceptionAsync(() => answering);

        return asked.Decisions[^1].Id;
    }
}
