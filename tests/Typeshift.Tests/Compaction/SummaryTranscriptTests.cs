using System.Text.Json;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Compaction.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Compaction;

public class SummaryTranscriptTests : AgentTestBase
{
    [Fact]
    public async Task Given_A_Tool_Call_Among_The_Summarized_Turns_The_Summary_Model_Reads_Its_Arguments()
    {
        // Arrange
        var settings = new CompactingAgent.Settings { MaxInputTokens = 1_000, SummaryModel = "summary-model" };
        var todos = JsonElement.Parse("""[{ "title": "Read the logs", "status": "InProgress" }]""");

        ChatClient
            .Reply(Call(Data.Tool.CallId, "write_todos", ("todos", todos)))
            .Reply(Text(new string('x', Page)))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(Data.Tool.Question, settings);

        // Act
        await RunAsync(Data.Agent.FollowUp, settings);

        // Assert
        ChatClient.Requests[2][1].Text.Should().MatchRegex(@"calls write_todos\(todos: \[.*Read the logs");
    }

    [Fact]
    public async Task Given_A_Cleared_Result_Among_The_Summarized_Turns_The_Summary_Model_Reads_Its_Start()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "read", ("length", Page)))
            .Reply(Call(Data.Tool.SecondCallId, "read", ("length", Page)))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 1_500, TargetInputTokens = 1_000 });

        // Assert
        ChatClient.Requests[2][1].Text.Should().Contain($"tool: result: {new string('x', 2_000)}…");
    }

    [Fact]
    public void Given_A_Calls_Arguments_Their_Text_Holds_Each_Value()
    {
        // Arrange
        var arguments = new Dictionary<string, object?>
        {
            ["path"] = JsonElement.Parse("\"notes.txt\""),
            ["lines"] = JsonElement.Parse("[1, 20]")
        };

        // Act
        var text = Tokens.Text(arguments);

        // Assert
        text.Should().Be("path: notes.txt, lines: [1, 20]");
    }

    private Task<ChatResponse> RunAsync(string input, CompactingAgent.Settings settings) =>
        Host<CompactingAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(input, settings: settings, ct: Ct);
}
