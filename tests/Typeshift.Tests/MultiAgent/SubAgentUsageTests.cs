using Typeshift.Tests.MultiAgent.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.MultiAgent;

public class SubAgentUsageTests : MultiAgentTestBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Sub_Agent_That_Called_Its_Model_The_Response_Reports_Its_Tokens_Too(bool streaming)
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)), Reported())
            .Reply(Text(Data.Tool.Answer), Reported());

        HelperClient.Reply(Text(Data.Agent.Reason), Reported());

        var lead = Host<LeadAgent>();

        // Act
        var response = streaming
            ? (await lead.RunStreamingAsync(Data.Tool.Question, cancellationToken: Ct).ToListAsync(Ct)).ToChatResponse()
            : await lead.RunAsync(Data.Tool.Question, cancellationToken: Ct);

        // Assert
        response.Usage!.InputTokenCount.Should().Be(3 * Data.Agent.InputTokens);
    }

    [Fact]
    public async Task Given_A_Sub_Agent_That_Called_Its_Model_The_Stored_Entries_Hold_Its_Tokens_Too()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)), Reported())
            .Reply(Text(Data.Tool.Answer), Reported());

        HelperClient.Reply(Text(Data.Agent.Reason), Reported());

        // Act
        await Host<LeadAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, cancellationToken: Ct);

        var entries = await EntriesAsync(Data.Agent.ConversationId);

        // Assert
        entries.Sum(entry => entry.Usage?.InputTokenCount ?? 0).Should().Be(3 * Data.Agent.InputTokens);
    }

    [Fact]
    public async Task Given_A_Background_Sub_Agent_The_Entries_Compaction_Calibrates_By_Hold_Only_Model_Reports()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)), Reported())
            .Reply(Text(Data.Agent.Steering), Reported())
            .Reply(Text(Data.Tool.Answer), Reported());

        HelperClient.Reply(gate, Text(Data.Agent.Reason), Reported());

        // Act
        var running = Host<BackgroundLeadAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, settings: new() { MaxInputTokens = 100_000 }, cancellationToken: Ct);

        await ChatClient.RequestedAsync(2);
        gate.SetResult();
        await running;

        // Assert
        var entries = await EntriesAsync(Data.Agent.ConversationId);
        entries.Where(entry => entry.EstimatedInputTokens is not null)
            .Sum(entry => entry.Usage?.InputTokenCount ?? 0)
            .Should()
            .Be(3 * Data.Agent.InputTokens);
    }

    [Fact]
    public async Task Given_A_Background_Sub_Agent_Its_Tokens_Are_Reported_Once_It_Answered()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)), Reported())
            .Reply(Text(Data.Agent.Steering), Reported())
            .Reply(Text(Data.Tool.Answer), Reported());

        HelperClient.Reply(gate, Text(Data.Agent.Reason), Reported());

        // Act
        var running = Host<BackgroundLeadAgent>().RunAsync(Data.Tool.Question, cancellationToken: Ct);
        await ChatClient.RequestedAsync(2);
        gate.SetResult();
        var response = await running;

        // Assert
        response.Usage!.InputTokenCount.Should().Be(4 * Data.Agent.InputTokens);
    }

    private ChatResponseUpdate Reported() =>
        new(
            ChatRole.Assistant,
            [
                new UsageContent(new UsageDetails
                {
                    InputTokenCount = Data.Agent.InputTokens,
                    OutputTokenCount = Data.Agent.OutputTokens
                })
            ]);
}
