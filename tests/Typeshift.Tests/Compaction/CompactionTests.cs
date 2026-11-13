using System.Text.Json;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Compaction.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Compaction;

public class CompactionTests : AgentTestBase
{
    private const string SummaryModel = "summary-model";

    private const string Cleared = Typeshift.Compaction.ClearedResult;

    [Fact]
    public async Task Given_Input_Over_The_Maximum_The_Oldest_Tool_Results_Are_Cleared_And_Their_Calls_Kept()
    {
        // Arrange
        ChatClient
            .Reply(Read(Data.Tool.CallId, Page))
            .Reply(Read(Data.Tool.SecondCallId, Page))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 2_000, TargetInputTokens = 1_500 });

        // Assert
        Results(2).Should().Equal(Cleared, new string('x', Page));
        Calls(2).Should().Equal(Data.Tool.CallId, Data.Tool.SecondCallId);
    }

    [Fact]
    public async Task Given_A_Cleared_Result_Later_Calls_Of_The_Run_Keep_It_Cleared()
    {
        // Arrange
        ChatClient
            .Reply(Read(Data.Tool.CallId, Page))
            .Reply(Read(Data.Tool.SecondCallId, Page))
            .Reply(Read($"{Data.Tool.CallId}_3", 4))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 2_000, TargetInputTokens = 1_500 });

        // Assert
        Results(3).Should().StartWith(Cleared);
    }

    [Fact]
    public async Task Given_No_Target_Compaction_Shrinks_To_Half_The_Maximum()
    {
        // Arrange
        ChatClient
            .Reply(Read(Data.Tool.CallId, Page))
            .Reply(Read(Data.Tool.SecondCallId, Page))
            .Reply(Read($"{Data.Tool.CallId}_3", Page))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 3_000 });

        // Assert
        Results(3).Should().Equal(Cleared, Cleared, new string('x', Page));
    }

    [Fact]
    public async Task Given_Input_Of_Exactly_The_Maximum_It_Is_Compacted()
    {
        // Arrange
        await ReadTwoPagesAsync();
        var estimate = Tokens.Estimate(ChatClient.Requests[2], ChatClient.Options[2]);

        // Act
        await ReadTwoPagesAsync(new() { MaxInputTokens = estimate, TargetInputTokens = estimate - 500 });

        // Assert
        Results(5)[0].Should().Be(Cleared);
    }

    [Fact]
    public async Task Given_Input_One_Token_Under_The_Maximum_Nothing_Is_Compacted()
    {
        // Arrange
        await ReadTwoPagesAsync();
        var estimate = Tokens.Estimate(ChatClient.Requests[2], ChatClient.Options[2]);

        // Act
        await ReadTwoPagesAsync(new() { MaxInputTokens = estimate + 1, TargetInputTokens = estimate - 500 });

        // Assert
        Results(5)[0].Should().Be(new string('x', Page));
    }

    [Fact]
    public async Task Given_Input_Over_The_Maximum_The_Latest_Todo_List_Keeps_Its_Result()
    {
        // Arrange
        var title = new string('t', Page / 10);
        var todos = JsonElement.Parse($$"""[{ "title": "{{title}}", "status": "InProgress" }]""");

        ChatClient
            .Reply(Call(Data.Tool.CallId, "write_todos", ("todos", todos)))
            .Reply(Read(Data.Tool.SecondCallId, Page))
            .Reply(Read($"{Data.Tool.CallId}_3", Page))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 2_000, TargetInputTokens = 1_500 });

        // Assert
        Results(3).Should().Equal($"InProgress: {title}", Cleared, new string('x', Page));
    }

    [Fact]
    public async Task Given_Clearing_Is_Not_Enough_The_Oldest_Turns_Are_Summarized()
    {
        // Arrange
        var settings = new CompactingAgent.Settings { MaxInputTokens = 1_000, SummaryModel = SummaryModel };

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(new string('x', Page), settings);

        // Act
        await RunAsync(Data.Agent.FollowUp, settings);

        // Assert
        Models.Should().Contain(new ProvidedModel(CompactingAgent.ModelProvider, SummaryModel));
        Texts(2).Should().HaveCount(3);
        Texts(2)[0].Should().EndWith(Data.Agent.Reason);
        Texts(2)[1..].Should().Equal(Data.Tool.Answer, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_No_Summary_Model_The_Runs_Model_Summarizes()
    {
        // Arrange
        var settings = new CompactingAgent.Settings { MaxInputTokens = 1_000 };

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(new string('x', Page), settings);

        // Act
        await RunAsync(Data.Agent.FollowUp, settings);

        // Assert
        Models.Should().AllBeEquivalentTo(new ProvidedModel(CompactingAgent.ModelProvider, CompactingAgent.Model));
        Texts(2)[0].Should().EndWith(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Summary_Provider_It_Serves_The_Summary_Model()
    {
        // Arrange
        var settings = new CompactingAgent.Settings
        {
            MaxInputTokens = 1_000,
            SummaryProvider = Data.Agent.Provider,
            SummaryModel = SummaryModel
        };

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(new string('x', Page), settings);

        // Act
        await RunAsync(Data.Agent.FollowUp, settings);

        // Assert
        Models.Should().Contain(new ProvidedModel(Data.Agent.Provider, SummaryModel));
    }

    [Fact]
    public async Task Given_A_Summary_Later_Runs_Start_From_It()
    {
        // Arrange
        var settings = new CompactingAgent.Settings { MaxInputTokens = 1_000, SummaryModel = SummaryModel };

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(new string('x', Page), settings);
        await RunAsync(Data.Agent.FollowUp, settings);

        // Act
        await RunAsync(Data.Agent.Steering);

        // Assert
        Texts(3)[0].Should().EndWith(Data.Agent.Reason);
        Texts(3)[1..].Should().Equal(Data.Tool.Answer, Data.Agent.FollowUp, Data.Tool.Answer, Data.Agent.Steering);
        (await EntriesAsync(Data.Agent.ConversationId)).Should().ContainSingle(entry => entry.IsSummary);
    }

    [Fact]
    public async Task Given_A_Steered_Streaming_Run_Its_Later_Pass_Starts_From_The_Summary()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        var settings = new CompactingAgent.Settings { MaxInputTokens = 1_000, SummaryModel = SummaryModel };

        ChatClient
            .Reply(Text(Data.Agent.CompanyName), Text(Data.Agent.Instructions), Read(Data.Tool.CallId, Page))
            .Reply(Text(Data.Agent.Reason))
            .Reply(gate, Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.FollowUp));

        var run = Host<CompactingAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(
                Data.Tool.Question,
                settings: settings,
                ct: Ct)
            .ToListAsync(Ct);

        await ChatClient.RequestedAsync(3);

        // Act
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        gate.SetResult();
        await run;

        // Assert
        Texts(3)[0].Should().EndWith(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_Summarized_Turns_The_Latest_Todo_List_Stays_With_Its_Result()
    {
        // Arrange
        var settings = new CompactingAgent.Settings { MaxInputTokens = 1_000, SummaryModel = SummaryModel };
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
        ChatClient.Requests[3].Select(message => message.Role.Value)
            .Should()
            .Equal("user", "assistant", "tool", "user");
        Calls(3).Should().Equal(Data.Tool.CallId);
    }

    [Fact]
    public async Task Given_A_Turn_Of_Several_Calls_The_Cut_Never_Parts_A_Call_From_Its_Result()
    {
        // Arrange
        ChatClient
            .Reply(Calls(Read(Data.Tool.CallId, Page), Read(Data.Tool.SecondCallId, Page)))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 1_000 });

        // Assert
        Roles(2).Should().Equal("user", "assistant", "tool");
        Calls(2).Should().Equal(Data.Tool.CallId, Data.Tool.SecondCallId);
        Results(2).Should().Equal(new string('x', Page), new string('x', Page));
    }

    [Fact]
    public async Task Given_A_Second_Compaction_Of_A_Run_The_First_Summary_Stays_Whole()
    {
        // Arrange
        var todos = JsonElement.Parse("""[{ "title": "Read the logs", "status": "InProgress" }]""");

        ChatClient
            .Reply(Call(Data.Tool.CallId, "write_todos", ("todos", todos)))
            .Reply(Read(Data.Tool.SecondCallId, Page))
            .Reply(Text(new string('s', Page)))
            .Reply(Read($"{Data.Tool.CallId}_3", 4))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 1_000 });

        // Assert
        ChatClient.Requests[4][1].Text.Should().Contain(new string('s', Page));
        ChatClient.Requests[4][1].Text.Should().Contain("write_todos");
        Roles(5).Should().Equal("user", "assistant", "tool", "assistant", "tool", "assistant", "tool");
    }

    [Fact]
    public async Task Given_Reported_Input_Tokens_Over_The_Estimate_Compaction_Goes_By_The_Report()
    {
        // Arrange
        var reported = new UsageDetails { InputTokenCount = 1_000 };

        ChatClient
            .Reply(
                Read(Data.Tool.CallId, Page / 2),
                new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(reported)]))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Read(Data.Tool.SecondCallId, 4))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 1_500, TargetInputTokens = 1_000 });

        // Assert
        Results(3)[0].Should().Be(Cleared);
    }

    [Fact]
    public async Task Given_A_Report_Of_The_Last_Run_The_Next_Runs_First_Call_Compacts_By_It()
    {
        // Arrange
        var reported = new UsageDetails { InputTokenCount = 2_000 };

        ChatClient
            .Reply(Read(Data.Tool.CallId, Page / 2))
            .Reply(Text(Data.Tool.Answer), new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(reported)]))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(Data.Tool.Question);

        // Act
        await RunAsync(Data.Agent.FollowUp, new() { MaxInputTokens = 1_500, TargetInputTokens = 1_000 });

        // Assert
        Results(2)[0].Should().Be(Cleared);
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Reported_Input_Tokens_Over_The_Estimate_Compaction_Goes_By_The_Report()
    {
        // Arrange
        var reported = new UsageDetails { InputTokenCount = 1_000 };

        ChatClient
            .Reply(
                Read(Data.Tool.CallId, Page / 2),
                new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(reported)]))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Read(Data.Tool.SecondCallId, 4))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<CompactingAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(
                Data.Tool.Question,
                settings: new() { MaxInputTokens = 1_500, TargetInputTokens = 1_000 },
                ct: Ct)
            .ToListAsync(Ct);

        // Assert
        Results(3)[0].Should().Be(Cleared);
    }

    [Fact]
    public void Given_A_Result_Loaded_From_The_Store_Its_Estimate_Is_The_Runs()
    {
        // Arrange
        var text = string.Concat(Enumerable.Repeat($"\"{Data.Persistence.TextOutsideAscii}\" ", 100));
        var result = JsonSerializer.SerializeToElement(text, Json.GetTypeInfo<string>());
        var message = new ChatMessage(ChatRole.Tool, [new FunctionResultContent(Data.Tool.CallId, result)]);
        var entry = new ConversationEntry("compacting_agent", [message], Time.GetUtcNow());

        // Act
        var loaded = ConversationEntry.Deserialize(entry.Serialize());

        // Assert
        Tokens.Estimate(loaded.Messages, null).Should().Be(Tokens.Estimate([message], null));
    }

    private Task<ChatResponse> RunAsync(string input, CompactingAgent.Settings? settings = null) =>
        Host<CompactingAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(input, settings: settings, ct: Ct);

    // A run of no conversation that reads two pages, one after the other, then answers: its third call holds both
    private async Task ReadTwoPagesAsync(CompactingAgent.Settings? settings = null)
    {
        ChatClient
            .Reply(Read(Data.Tool.CallId, Page))
            .Reply(Read(Data.Tool.SecondCallId, Page))
            .Reply(Text(Data.Tool.Answer));

        await Host<CompactingAgent>().RunAsync(Data.Tool.Question, settings: settings, ct: Ct);
    }

    private ChatResponseUpdate Read(string callId, int length) => Call(callId, "read", ("length", length));

    private List<string> Roles(int request) =>
        [.. ChatClient.Requests[request].Select(message => message.Role.Value)];

    private List<string> Results(int request) =>
    [
        .. ChatClient.Requests[request]
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Select(result => result.Result is JsonElement json ? json.GetString()! : $"{result.Result}")
    ];

    private List<string> Calls(int request) =>
    [
        .. ChatClient.Requests[request]
            .SelectMany(message => message.Contents)
            .OfType<FunctionCallContent>()
            .Select(call => call.CallId)
    ];

    private List<string> Texts(int request) =>
        [.. ChatClient.Requests[request].Select(message => message.Text)];
}
