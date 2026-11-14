using Typeshift.Tests.Approval.Support;
using Typeshift.Tests.Decisions.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Typeshift.Tests.Decisions;

// The two behaviors of Microsoft.Extensions.AI's tool loop the decision system relies on: it invokes no call marked
// as informational, and a layer below it may answer in the model's place. An upgrade that changes either fails here
public class ToolLoopGuardTests : ApprovalTestBase
{
    [Fact]
    public async Task Given_A_Response_Whose_Calls_Wait_The_Tool_Loop_Invokes_None_Of_Them_And_Calls_No_Model_Again()
    {
        // Arrange
        ChatClient.Reply(Calls(
            CloseTicket(Data.Tool.CallId, Data.Agent.Ticket),
            Note(Data.Tool.SecondCallId, Data.Tool.Query)));

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        ToolCalls.Should().BeEmpty();
        ChatClient.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Streamed_Response_Whose_Calls_Wait_The_Tool_Loop_Invokes_None_Of_Them()
    {
        // Arrange
        ChatClient.Reply(
            Text(Data.Agent.FollowUp),
            CloseTicket(Data.Tool.CallId, Data.Agent.Ticket),
            Note(Data.Tool.SecondCallId, Data.Tool.Query));

        // Act
        await StreamAsync(Data.Tool.Question);

        // Assert
        ToolCalls.Should().BeEmpty();
        ChatClient.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_An_Answered_Batch_Its_Calls_Run_Without_A_Model_Call_Of_Their_Own()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);

        // Act
        await AnswerAsync(DecisionAnswer.Approve(asked.Decisions[0].Id));

        // Assert
        ToolCalls.Should().ContainSingle();
        ChatClient.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_An_Answered_Batch_In_A_Streaming_Run_Its_Calls_Run_Without_A_Model_Call_Of_Their_Own()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var asked = await StreamAsync(Data.Tool.Question);

        // Act
        await StreamAnswerAsync(DecisionAnswer.Approve(asked.Decisions[0].Id));

        // Assert
        ToolCalls.Should().ContainSingle();
        ChatClient.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_Tools_That_Require_Approval_None_The_Model_Is_Offered_Makes_The_Tool_Loop_Pause()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question);
        var offered = ChatClient.Options[0]!.Tools!;
        var pausing = offered.Where(tool => tool.GetService<ApprovalRequiredAIFunction>() is not null);

        // Assert
        offered.Select(tool => tool.Name).Should().Contain("close_ticket");
        pausing.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Call_Of_A_Tool_That_Requires_Approval_Without_A_Verdict_The_Invoker_Does_Not_Run_It()
    {
        // Arrange
        var ran = false;
        var tool = AIFunctionFactory.Create(() => ran = true, "close_ticket");
        var gate = new ToolGate([], NullLogger.Instance);

        var call = new FunctionInvocationContext
        {
            Function = (AIFunction)GatedFunction.Wrap(new ApprovalRequiredAIFunction(tool)),
            CallContent = new FunctionCallContent(Data.Tool.CallId, "close_ticket"),
            Options = new ChatOptions()
        };

        // Act
        var result = await gate.InvokeAsync(call, Ct);

        // Assert
        ran.Should().BeFalse();
        result.Should().Be(DecisionResults.NotApproved);
    }

    [Fact]
    public async Task Given_A_Provider_Client_That_Hides_Its_Own_Tool_Loop_A_Tool_That_Requires_Approval_Does_Not_Run()
    {
        // Arrange
        var calls = new ToolCalls();
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<InMemoryTypeshiftStore<Guid>>();
        services.AddChatClient(new OpaqueChatClient(ChatClient.AsBuilder().UseFunctionInvocation().Build()));
        services.AddSingleton(calls);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await scope.ServiceProvider.GetRequiredService<AgentHost<GuardedAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        calls.Should().BeEmpty();
        ResultSentToModel(1).Should().Be(DecisionResults.NotApproved);
    }

    [Fact]
    public async Task Given_Runs_That_Ask_And_Continue_No_Model_Request_Carries_A_Request_For_Input_Or_Its_Answer()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);
        await AnswerAsync(DecisionAnswer.Approve(asked.Decisions[0].Id));

        // Act
        await RunAsync(Data.Agent.FollowUp);
        var sent = ChatClient.Requests.SelectMany(request => request).SelectMany(message => message.Contents);

        // Assert
        sent.Should().NotContain(content => content is InputRequestContent || content is InputResponseContent);
    }

    [Fact]
    public async Task Given_A_Plan_And_A_Call_Your_App_Approved_No_Stored_Entry_Carries_A_Request_Or_Its_Answer()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "exit_plan_mode", ("plan", Data.Decisions.Plan)))
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var planned = await RunAsync(Data.Tool.Question, AgentMode.Plan);
        var asked = await AnswerAsync(DecisionAnswer.ApprovePlan(planned.Decisions[0].Id, AgentMode.Default));
        await AnswerAsync(DecisionAnswer.Approve(asked.Decisions[0].Id));

        // Act
        var entries = await EntriesAsync(Data.Agent.ConversationId);
        var stored = entries.SelectMany(entry => entry.Messages).SelectMany(message => message.Contents);
        var sent = ChatClient.Requests.SelectMany(request => request).SelectMany(message => message.Contents);

        // Assert
        stored.Should().NotContain(content => content is InputRequestContent || content is InputResponseContent);
        sent.Should().NotContain(content => content is InputRequestContent || content is InputResponseContent);
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }
}
