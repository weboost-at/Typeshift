using Typeshift.Tests.MultiAgent.Support;
using Typeshift.Tests.Testing.Fakes;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.MultiAgent;

// The lead starts its helper in the background, then calls close_ticket, which waits for your app; the helper answers
// only after that, while the decision is open
public class OpenDecisionBackgroundTests : MultiAgentTestBase
{
    private readonly TaskCompletionSource _helper = new();

    [Fact]
    public async Task Given_An_Open_Decision_A_Background_Answer_Starts_No_Model_Call()
    {
        // Arrange
        ScriptADecisionWhileTheHelperWorks();

        // Act
        var asked = await AskAsync();

        // Assert
        asked.Decisions.Should().ContainSingle();
        ChatClient.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_An_Open_Decision_The_Background_Answer_Is_Stored_Ahead_Of_The_Runs_End()
    {
        // Arrange
        ScriptADecisionWhileTheHelperWorks();

        // Act
        await AskAsync();
        var entries = await EntriesAsync(Data.Agent.ConversationId);

        // Assert
        entries[^1].EndsRun.Should().BeTrue();
        entries[^2].Messages.Single().Text.Should().StartWith("The background agent helper_agent answered");
    }

    [Fact]
    public async Task Given_The_Answer_To_The_Decision_The_Call_Runs_Once_And_The_Model_Reads_The_Background_Answer()
    {
        // Arrange
        ScriptADecisionWhileTheHelperWorks().Reply(Text(Data.Tool.Answer));
        var asked = await AskAsync();

        // Act
        await Lead.AnswerAsync([DecisionAnswer.Approve(asked.Decisions.Single().Id)], ct: Ct);
        var sent = ChatClient.Requests[^1];

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
        sent.Select(message => message.Text).Should().Contain(text => text.StartsWith("The background agent"));
        sent.SelectMany(message => message.Contents).OfType<FunctionCallContent>().Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_A_Streaming_Run_And_An_Open_Decision_The_Answered_Call_Runs_Once()
    {
        // Arrange
        ScriptADecisionWhileTheHelperWorks().Reply(Text(Data.Tool.Answer));

        var running = Lead.RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct).AsTask();
        await ChatClient.RequestedAsync(2).WaitAsync(Ct);
        _helper.SetResult();
        var asked = (await running).ToChatResponse();

        // Act
        await Lead.AnswerStreamingAsync([DecisionAnswer.Approve(asked.Decisions.Single().Id)], ct: Ct).ToListAsync(Ct);

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
        ChatClient.Requests.Should().HaveCount(3);
    }

    private AgentHost<GuardedBackgroundLeadAgent> Lead =>
        Host<GuardedBackgroundLeadAgent>().WithConversationId(Data.Agent.ConversationId);

    private ScriptedChatClient ScriptADecisionWhileTheHelperWorks()
    {
        HelperClient.Reply(_helper, Text(Data.Agent.Reason));

        return ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Call(Data.Tool.SecondCallId, "close_ticket", ("number", Data.Agent.Ticket.Default)));
    }

    // The helper answers once the model asked for close_ticket, so its answer finds the decision open
    private async Task<ChatResponse> AskAsync()
    {
        var running = Lead.RunAsync(Data.Tool.Question, ct: Ct);
        await ChatClient.RequestedAsync(2).WaitAsync(Ct);
        _helper.SetResult();

        return await running;
    }
}
