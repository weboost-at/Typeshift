using Typeshift.Tests.MultiAgent.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.MultiAgent;

public class SubAgentTests : MultiAgentTestBase
{
    [Fact]
    public async Task Given_A_Sub_Agent_It_Is_Offered_With_Its_Description_And_One_Input()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<LeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        var tool = ChatClient.Options[0]!.Tools!.OfType<AIFunctionDeclaration>().Single();
        tool.Name.Should().Be("helper_agent");
        tool.Description.Should().Be(HelperAgent.About);
        tool.JsonSchema.GetProperty("required")
            .EnumerateArray()
            .Select(name => name.GetString())
            .Should()
            .Equal("input");
    }

    [Fact]
    public async Task Given_A_Sub_Agent_It_Answers_In_A_Fresh_Context_And_Its_Answer_Is_The_Result()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(Text(Data.Agent.Reason));

        // Act
        var response = await Host<LeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
        HelperClient.Requests[0].Select(message => message.Text).Should().Equal(Data.Agent.FollowUp);
        Result(ChatClient.Requests[1]).Should().Be(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Sub_Agent_In_The_Default_Mode_A_Call_That_Needs_Approval_Is_Rejected()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient
            .Reply(Call(Data.Tool.SecondCallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Text(Data.Agent.Reason));

        // Act
        await Host<LeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ToolCalls.Should().BeEmpty();
        Result(HelperClient.Requests[1]).Should().Contain("sub-agent");
    }

    [Fact]
    public async Task Given_A_Caller_In_Auto_Mode_Its_Sub_Agent_Runs_In_Auto_Mode()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient
            .Reply(Call(Data.Tool.SecondCallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Text(Data.Agent.Reason));

        // Act
        await Host<LeadAgent>().RunAsync(Data.Tool.Question, settings: new() { Mode = AgentMode.Auto }, ct: Ct);

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_A_Background_Sub_Agent_The_Caller_Works_On_And_Receives_Its_Answer_Before_It_Ends()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Agent.Steering))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(gate, Text(Data.Agent.Reason));

        // Act
        var running = Host<BackgroundLeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);
        await ChatClient.RequestedAsync(2);
        gate.SetResult();
        var response = await running;

        // Assert
        ChatClient.Requests[2][^1].Text.Should().EndWith(Data.Agent.Reason);
        response.Messages[^1].Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_An_Interrupted_Run_With_A_Background_Sub_Agent_The_Model_Hears_It_Was_Lost()
    {
        // Arrange
        var store = Services.GetRequiredService<ITypeshiftStore>();
        var conversation = Data.Agent.ConversationId;

        ChatMessage[] steps =
        [
            new(ChatRole.User, Data.Tool.Question),
            new(ChatRole.Assistant, [new FunctionCallContent(Data.Tool.CallId, "helper_agent")]),
            new(ChatRole.Tool, [new FunctionResultContent(Data.Tool.CallId, "started")])
        ];

        await store.AppendConversationAsync(conversation, new("background_lead_agent", steps, Time.GetUtcNow()), Ct);
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<BackgroundLeadAgent>().ResumeAsync(conversation, ct: Ct);

        // Assert
        ChatClient.Requests[0][^1].Text.Should().Contain("helper_agent was lost");
    }

    [Fact]
    public async Task Given_A_Cancelled_Run_With_A_Background_Sub_Agent_The_Next_Run_Hears_It_Was_Lost()
    {
        // Arrange
        var store = Services.GetRequiredService<ITypeshiftStore>();
        var conversation = Data.Agent.ConversationId;

        ChatMessage[] steps =
        [
            new(ChatRole.User, Data.Tool.Question),
            new(ChatRole.Assistant, [new FunctionCallContent(Data.Tool.CallId, "helper_agent")]),
            new(ChatRole.Tool, [new FunctionResultContent(Data.Tool.CallId, "started")])
        ];

        await store.AppendConversationAsync(conversation, new("background_lead_agent", steps, Time.GetUtcNow()), Ct);
        var end = new ConversationEntry("background_lead_agent", [], Time.GetUtcNow()) { EndsRun = true };

        await store.AppendConversationAsync(conversation, end, Ct);
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<BackgroundLeadAgent>().RunAsync(Data.Agent.FollowUp, conversationId: conversation, ct: Ct);

        // Assert
        ChatClient.Requests[0].Select(message => message.Text).Should().Contain(text => text.Contains("was lost"));
    }

    private static string? Result(IEnumerable<ChatMessage> request) =>
        request.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Single().Result?.ToString();
}
