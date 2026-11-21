#pragma warning disable TYPESHIFT001 // a summary goes under the run's model policy, which is experimental
using System.Net;
using System.Text.Json;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Compaction.Support;
using Typeshift.Tests.Hosting.Support;
using Typeshift.Tests.Testing.Fakes;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Typeshift.Tests.Compaction;

public class CompactionTests : AgentTestBase
{
    private const string SummaryModel = "summary-model";

    private Action<IServiceCollection> _register = _ => { };

    private const string Cleared = Typeshift.Compaction.ClearedReadOnlyResult;

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
        ChatClient.Assert.Request(2).Results.Should().Equal(Cleared, new string('x', Page));
        ChatClient.Assert.Request(2).Calls.Should().Equal(Data.Tool.CallId, Data.Tool.SecondCallId);
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
        ChatClient.Assert.Request(3).Results.Should().StartWith(Cleared);
    }

    [Fact]
    public async Task Given_Results_Shorter_Than_The_Placeholder_They_Stay()
    {
        // Arrange
        ChatClient
            .Reply(Read(Data.Tool.CallId, 2))
            .Reply(Read(Data.Tool.SecondCallId, 2))
            .Reply(Read($"{Data.Tool.CallId}_3", Page))
            .Reply(Read($"{Data.Tool.CallId}_4", Page))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 2_000, TargetInputTokens = 1_500 });

        // Assert
        ChatClient.Assert.Request(4).Results.Should().Equal("xx", "xx", Cleared, new string('x', Page));
    }

    [Fact]
    public async Task Given_A_Cleared_Result_Of_A_Tool_Not_Read_Only_The_Placeholder_Does_Not_Ask_To_Call_Again()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "send", ("length", Page)))
            .Reply(Call(Data.Tool.SecondCallId, "send", ("length", Page)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 2_000, TargetInputTokens = 1_500 });

        // Assert
        ChatClient.Assert.Request(2).Results.Should().Equal("[Cleared to save context.]", new string('y', Page));
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
        ChatClient.Assert.Request(3).Results.Should().Equal(Cleared, Cleared, new string('x', Page));
    }

    [Fact]
    public async Task Given_Input_Of_Exactly_The_Maximum_It_Is_Compacted()
    {
        // Arrange
        await ReadTwoPagesAsync();
        var estimate = Estimate(2);

        // Act
        await ReadTwoPagesAsync(new() { MaxInputTokens = estimate, TargetInputTokens = estimate - 500 });

        // Assert
        ChatClient.Assert.Request(5).Results[0].Should().Be(Cleared);
    }

    [Fact]
    public async Task Given_Input_One_Token_Under_The_Maximum_Nothing_Is_Compacted()
    {
        // Arrange
        await ReadTwoPagesAsync();
        var estimate = Estimate(2);

        // Act
        await ReadTwoPagesAsync(new() { MaxInputTokens = estimate + 1, TargetInputTokens = estimate - 500 });

        // Assert
        ChatClient.Assert.Request(5).Results[0].Should().Be(new string('x', Page));
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
        ChatClient.Assert.Request(3).Results.Should().Equal($"InProgress: {title}", Cleared, new string('x', Page));
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
        ChatClient.Assert.Request(2).Texts.Should().HaveCount(3);
        ChatClient.Assert.Request(2).Texts[0].Should().EndWith(Data.Agent.Reason);
        ChatClient.Assert.Request(2).Texts[1..].Should().Equal(Data.Tool.Answer, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_A_Summary_Call_The_Response_Reports_Its_Tokens_Too()
    {
        // Arrange
        var settings = new CompactingAgent.Settings { MaxInputTokens = 1_000, SummaryModel = SummaryModel };

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason), Reported())
            .Reply(Text(Data.Tool.Answer), Reported());

        await RunAsync(new string('x', Page), settings);

        // Act
        var response = await RunAsync(Data.Agent.FollowUp, settings);

        // Assert
        response.Usage!.InputTokenCount.Should().Be(2 * Data.Agent.InputTokens);
    }

    [Fact]
    public async Task Given_A_Summary_Call_The_Summarys_Entry_Holds_Its_Tokens()
    {
        // Arrange
        var settings = new CompactingAgent.Settings { MaxInputTokens = 1_000, SummaryModel = SummaryModel };

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason), Reported())
            .Reply(Text(Data.Tool.Answer), Reported());

        await RunAsync(new string('x', Page), settings);

        // Act
        await RunAsync(Data.Agent.FollowUp, settings);

        // Assert
        var summary = (await EntriesAsync(Data.Agent.ConversationId)).Single(entry => entry.IsSummary);
        summary.Usage!.InputTokenCount.Should().Be(Data.Agent.InputTokens);
    }

    [Fact]
    public async Task Given_A_Message_Without_Contents_Where_The_Cut_Would_Fall_The_Cut_Passes_It_By()
    {
        // Arrange
        var store = Services.GetRequiredService<ITypeshiftStore<Guid>>();
        var settings = new CompactingAgent.Settings { MaxInputTokens = 1_000, SummaryModel = SummaryModel };

        ConversationEntry[] earlier =
        [
            new()
            {
                Agent = "compacting_agent",
                Messages = [new ChatMessage(ChatRole.User, new string('x', Page))],
                At = Time.GetUtcNow()
            },
            new()
            {
                Agent = "compacting_agent",
                Messages = [new ChatMessage(ChatRole.Assistant, [])],
                At = Time.GetUtcNow()
            },
            new() { Agent = "compacting_agent", At = Time.GetUtcNow(), EndsRun = true }
        ];

        foreach (var entry in earlier)
        {
            await store.AppendConversationAsync(Data.Agent.ConversationId, entry, Ct);
        }

        ChatClient
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await RunAsync(Data.Agent.FollowUp, settings);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Report_For_A_Call_Estimated_At_Nothing_The_Next_Call_Of_The_Run_Is_Not_Calibrated_By_It()
    {
        // Arrange
        var answering = new TaskCompletionSource();
        var usage = new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(new() { InputTokenCount = Page })]);

        ChatClient
            .Reply(answering, Text("ok"), usage)
            .Reply(Text(Data.Tool.Answer));

        var run = Host<LimitsAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync("hi", LimitsAgent.Limit(nameof(RunOptions.MaxInputTokens), 10), Ct);

        await ChatClient.Requested;
        Tracker.TrySteer(Data.Agent.ConversationId, "and more");

        // Act
        answering.SetResult();
        await run;

        // Assert
        ChatClient.Requests.Should().HaveCount(2);
    }

    [Fact]
    public void Given_An_Attachment_It_Is_Estimated_At_A_Thousand_Tokens_Whatever_Its_Size()
    {
        // Act
        var estimate = Tokens.Estimate(new DataContent(new byte[4], "image/png"));

        // Assert
        estimate.Should().Be(1_000);
    }

    [Theory]
    [InlineData(2_000)]
    [InlineData(1_000)]
    public void Given_A_Target_Not_Below_The_Maximum_The_Agent_Fails_To_Declare(int target)
    {
        // Act
        var declare = () => new LimitsAgent(iterations: 40, errors: 3, maximum: 1_000, target);

        // Assert
        declare.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage("TargetInputTokens must be below MaxInputTokens, 1000.*");
    }

    [Fact]
    public async Task Given_Summary_Instructions_Of_The_Agents_Own_The_Summary_Model_Receives_Them()
    {
        // Arrange
        var settings = new CompactingAgent.Settings { MaxInputTokens = 1_000, SummaryInstructions = Data.Agent.Reason };

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Steering))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(new string('x', Page), settings);

        // Act
        await RunAsync(Data.Agent.FollowUp, settings);

        // Assert
        ChatClient.Requests[1][0].Text.Should().Be(Data.Agent.Reason);
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
        ChatClient.Assert.Request(2).Texts[0].Should().EndWith(Data.Agent.Reason);
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
    public async Task Given_A_Provider_At_The_Call_Site_It_Serves_The_Summary_Over_A_Summary_Provider()
    {
        // Arrange
        var callSite = CreateScriptedClient();
        _register = services => services.AddKeyedChatClient(Vendor.Other, callSite);

        var settings = new CompactingAgent.Settings
        {
            MaxInputTokens = 1_000,
            SummaryProvider = Data.Agent.Provider,
            SummaryModel = SummaryModel
        };

        callSite
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        var host = Host<CompactingAgent>().WithConversationId(Data.Agent.ConversationId).WithProvider(Vendor.Other);
        await host.RunAsync(new string('x', Page), settings, Ct);

        // Act
        await host.RunAsync(Data.Agent.FollowUp, settings, Ct);

        // Assert
        callSite.Options[1]!.ModelId.Should().Be(SummaryModel);
    }

    [Fact]
    public async Task Given_A_Summary_Model_That_Fails_The_Runs_Own_Model_Summarizes()
    {
        // Arrange
        var settings = new CompactingAgent.Settings { MaxInputTokens = 1_000, SummaryModel = SummaryModel };

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(new HttpRequestException(Data.Agent.Instructions))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(new string('x', Page), settings);

        // Act
        await RunAsync(Data.Agent.FollowUp, settings);

        // Assert
        Models.Select(asked => asked.Model).Should().Equal(
            CompactingAgent.Model,
            SummaryModel,
            CompactingAgent.Model,
            CompactingAgent.Model);
        ChatClient.Assert.Request(3).Texts[0].Should().EndWith(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Summary_That_Fails_On_Every_Model_The_Call_Goes_Out_Cleared()
    {
        // Arrange
        var settings = new CompactingAgent.Settings
        {
            MaxInputTokens = 1_500,
            TargetInputTokens = 1_000,
            SummaryModel = SummaryModel
        };

        ChatClient
            .Reply(Read(Data.Tool.CallId, Page))
            .Reply(Read(Data.Tool.SecondCallId, Page))
            .Reply(new HttpRequestException(Data.Agent.Instructions))
            .Reply(new HttpRequestException(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await RunAsync(Data.Tool.Question, settings);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
        ChatClient.Assert.Request(4).Texts[0].Should().Be(Data.Tool.Question);
        ChatClient.Assert.Request(4).Results.Should().Equal(Cleared, new string('x', Page));
    }

    [Fact]
    public async Task Given_No_Summary_Model_A_Summary_That_Fails_Is_Asked_For_Once()
    {
        // Arrange
        ChatClient
            .Reply(Read(Data.Tool.CallId, Page))
            .Reply(Read(Data.Tool.SecondCallId, Page))
            .Reply(new HttpRequestException(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 1_500, TargetInputTokens = 1_000 });

        // Assert
        ChatClient.Requests.Should().HaveCount(4);
        ChatClient.Assert.Request(3).Results.Should().Equal(Cleared, new string('x', Page));
    }

    [Fact]
    public async Task Given_A_Fallback_Of_The_Run_Its_Summary_Runs_On_The_Pair_The_Run_Is_On()
    {
        // Arrange
        var policy = ModelPolicy.Handle(ModelFailure.Unavailable).FallbackTo(Data.Agent.Provider, Data.Agent.Model);

        ChatClient
            .Reply(Unavailable())
            .Reply(Read(Data.Tool.CallId, Page))
            .Reply(Read(Data.Tool.SecondCallId, Page))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 1_500, TargetInputTokens = 1_000 }, policy);

        // Assert
        Models[3].Should().Be(new ProvidedModel(Data.Agent.Provider, Data.Agent.Model));
    }

    [Fact]
    public async Task Given_A_Fallback_To_A_Provider_The_Summary_Model_Goes_To_That_Provider()
    {
        // Arrange
        var policy = ModelPolicy.Handle(ModelFailure.Unavailable).FallbackToProvider(Data.Agent.Provider);

        var settings = new CompactingAgent.Settings
        {
            MaxInputTokens = 1_500,
            TargetInputTokens = 1_000,
            SummaryModel = SummaryModel
        };

        ChatClient
            .Reply(Unavailable())
            .Reply(Read(Data.Tool.CallId, Page))
            .Reply(Read(Data.Tool.SecondCallId, Page))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, settings, policy);

        // Assert
        Models[3].Should().Be(new ProvidedModel(Data.Agent.Provider, SummaryModel));
    }

    [Fact]
    public async Task Given_A_Summary_Call_That_Fails_The_Runs_Model_Policy_Retries_It()
    {
        // Arrange
        var policy = ModelPolicy.Handle(ModelFailure.Unavailable).Retry(1);

        ChatClient
            .Reply(Read(Data.Tool.CallId, Page))
            .Reply(Read(Data.Tool.SecondCallId, Page))
            .Reply(Unavailable())
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 1_500, TargetInputTokens = 1_000 }, policy);

        // Assert
        ChatClient.Assert.Request(4).Texts[0].Should().EndWith(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Summary_Call_The_Policy_Moves_Elsewhere_The_Runs_Own_Calls_Stay_On_Their_Pair()
    {
        // Arrange
        var policy = ModelPolicy.Handle(ModelFailure.Unavailable).FallbackToModel(Data.Agent.Model);

        ChatClient
            .Reply(Read(Data.Tool.CallId, Page))
            .Reply(Read(Data.Tool.SecondCallId, Page))
            .Reply(Unavailable())
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 1_500, TargetInputTokens = 1_000 }, policy);

        // Assert
        Models.Select(asked => asked.Model).Should().Equal(
            CompactingAgent.Model,
            CompactingAgent.Model,
            CompactingAgent.Model,
            Data.Agent.Model,
            CompactingAgent.Model);
    }

    [Fact]
    public async Task Given_A_Run_Cancelled_While_Its_Summary_Is_Written_The_Run_Ends_Cancelled()
    {
        // Arrange
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var settings = new CompactingAgent.Settings { MaxInputTokens = 1_000, SummaryModel = SummaryModel };

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(new TaskCompletionSource());

        await RunAsync(new string('x', Page), settings);

        var run = Host<CompactingAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Agent.FollowUp, settings: settings, ct: stopping.Token);

        await ChatClient.RequestedAsync(2);

        // Act
        await stopping.CancelAsync();
        var ending = () => run;

        // Assert
        await ending.Should().ThrowAsync<OperationCanceledException>();
        ChatClient.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_A_Summary_Model_That_Fails_A_Warning_Names_It_And_What_Happens_Instead()
    {
        // Arrange
        var logs = new WarningLog();
        var services = new ServiceCollection();
        services.AddTypeshift().WithScriptedProviders();
        services.AddSingleton(ChatClient).AddSingleton(Models).AddSingleton<ILoggerFactory>(logs);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<CompactingAgent>>();

        ChatClient
            .Reply(Read(Data.Tool.CallId, Page))
            .Reply(new HttpRequestException(Data.Agent.Instructions))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await host.RunAsync(
            Data.Tool.Question,
            settings: new() { MaxInputTokens = 1_000, SummaryModel = SummaryModel },
            ct: Ct);

        // Assert
        logs.Warnings.Should().ContainSingle();
        logs.Warnings[0].Should().Contain(SummaryModel);
        logs.Warnings[0].Should().EndWith("its own model writes the summary instead.");
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
        ChatClient.Assert.Request(3).Texts[0].Should().EndWith(Data.Agent.Reason);
        ChatClient.Assert.Request(3)
            .Texts[1..]
            .Should()
            .Equal(Data.Tool.Answer, Data.Agent.FollowUp, Data.Tool.Answer, Data.Agent.Steering);
        (await EntriesAsync(Data.Agent.ConversationId)).Should().ContainSingle(entry => entry.IsSummary);
    }

    [Fact]
    public async Task Given_A_Watcher_A_Compaction_Publishes_Nothing_Of_Its_Summary()
    {
        // Arrange
        var settings = new CompactingAgent.Settings { MaxInputTokens = 1_000, SummaryModel = SummaryModel };

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Agent.CompanyName));

        await RunAsync(new string('x', Page), settings);

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var watch = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).GetAsyncEnumerator(Ct);
        var first = watch.MoveNextAsync();

        // Act
        await RunAsync(Data.Agent.FollowUp, settings);

        // Assert
        (await first).Should().BeTrue();
        watch.Current.Text.Should().Be(Data.Agent.CompanyName);
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
        ChatClient.Assert.Request(3).Texts[0].Should().EndWith(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_Turns_To_Summarize_The_Kept_Turns_Fit_With_The_Instructions_And_The_Tools()
    {
        // Arrange
        ChatClient
            .Reply(Text(new string('y', Page / 2)))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(new string('x', Page / 2));

        // Act
        await RunAsync(Data.Agent.FollowUp, new() { MaxInputTokens = 1_000, TargetInputTokens = 600 });

        // Assert
        ChatClient.Assert.Request(2).Texts[1..].Should().Equal(Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_Turns_To_Summarize_The_Kept_Turns_Leave_Room_For_The_Summary()
    {
        // Arrange
        var answer = new ChatMessage(ChatRole.Assistant, new string('y', Page / 2));
        var followUp = new ChatMessage(ChatRole.User, Data.Agent.FollowUp);

        ChatClient
            .Reply(Text(answer.Text))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(new string('x', Page));
        var fitting = Tokens.Estimate([answer, followUp], ChatClient.Options[0]);

        // Act
        await RunAsync(
            Data.Agent.FollowUp,
            new() { MaxInputTokens = fitting + 500, TargetInputTokens = fitting + (fitting / 20) });

        // Assert
        ChatClient.Assert.Request(2).Texts[1..].Should().Equal(Data.Agent.FollowUp);
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
        ChatClient.Assert.Request(3).Calls.Should().Equal(Data.Tool.CallId);
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
        ChatClient.Assert.Request(2).Roles.Should().Equal("user", "assistant", "tool");
        ChatClient.Assert.Request(2).Calls.Should().Equal(Data.Tool.CallId, Data.Tool.SecondCallId);
        ChatClient.Assert.Request(2).Results.Should().Equal(new string('x', Page), new string('x', Page));
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
        ChatClient.Assert.Request(5)
            .Roles
            .Should()
            .Equal("user", "assistant", "tool", "assistant", "tool", "assistant", "tool");
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
        ChatClient.Assert.Request(3).Results[0].Should().Be(Cleared);
    }

    [Fact]
    public async Task Given_A_Report_Of_The_Last_Run_The_Next_Runs_First_Call_Compacts_By_It()
    {
        // Arrange
        var settings = new CompactingAgent.Settings { MaxInputTokens = 1_500, TargetInputTokens = 1_000 };
        var reported = new UsageDetails { InputTokenCount = 2_000 };

        ChatClient
            .Reply(Read(Data.Tool.CallId, Page / 2))
            .Reply(Text(Data.Tool.Answer), new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(reported)]))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(Data.Tool.Question, settings);

        // Act
        await RunAsync(Data.Agent.FollowUp, settings);

        // Assert
        ChatClient.Assert.Request(2).Results[0].Should().Be(Cleared);
    }

    [Fact]
    public async Task Given_A_Run_That_Cleared_Results_The_Next_Runs_First_Call_Stays_Under_The_Maximum()
    {
        // Arrange
        var settings = new CompactingAgent.Settings { MaxInputTokens = 2_000, TargetInputTokens = 1_500 };
        var reported = new UsageDetails { InputTokenCount = 1_200 };

        ChatClient
            .Reply(Read(Data.Tool.CallId, Page))
            .Reply(Read(Data.Tool.SecondCallId, Page))
            .Reply(Read($"{Data.Tool.CallId}_3", Page))
            .Reply(Text(Data.Tool.Answer), new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(reported)]))
            .Reply(Text(Data.Agent.Reason));

        await RunAsync(Data.Tool.Question, settings);

        // Act
        await RunAsync(Data.Agent.FollowUp, settings);

        // Assert
        Estimate(4).Should().BeLessThan(2_000);
    }

    [Fact]
    public async Task Given_A_Maximum_Each_Response_Is_Stored_With_The_Estimate_Of_Its_Call()
    {
        // Arrange
        ChatClient
            .Reply(Read(Data.Tool.CallId, Page))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 2_000 });

        // Assert
        (await EntriesAsync(Data.Agent.ConversationId)).Select(entry => entry.EstimatedInputTokens)
            .Should()
            .Equal(null, Estimate(0), null, Estimate(1), null);
    }

    [Fact]
    public async Task Given_A_Report_Stored_Without_An_Estimate_The_Next_Runs_First_Call_Goes_By_The_Estimate_Alone()
    {
        // Arrange
        var store = Services.GetRequiredService<ITypeshiftStore<Guid>>();

        foreach (var entry in StoredRuns.ReportedWithoutEstimate(new string('x', Page / 2)))
        {
            await store.AppendConversationAsync(Data.Agent.ConversationId, entry, Ct);
        }

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Agent.FollowUp, new() { MaxInputTokens = 1_500, TargetInputTokens = 1_000 });

        // Assert
        ChatClient.Assert.Request(0).Results[0].Should().Be(new string('x', Page / 2));
    }

    [Fact]
    public async Task Given_A_Stored_Estimate_Of_Zero_The_Next_Runs_First_Call_Goes_By_The_Estimate_Alone()
    {
        // Arrange
        var store = Services.GetRequiredService<ITypeshiftStore<Guid>>();
        var nothing = new UsageDetails { InputTokenCount = 0 };

        foreach (var entry in StoredRuns.ReportedWithoutEstimate(new string('x', Page / 2)))
        {
            var stored = entry.Usage is null ? entry : entry with { Usage = nothing, EstimatedInputTokens = 0 };
            await store.AppendConversationAsync(Data.Agent.ConversationId, stored, Ct);
        }

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Agent.FollowUp, new() { MaxInputTokens = 1_500, TargetInputTokens = 1_000 });

        // Assert
        ChatClient.Assert.Request(0).Results[0].Should().Be(new string('x', Page / 2));
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
        ChatClient.Assert.Request(3).Results[0].Should().Be(Cleared);
    }

    [Fact]
    public void Given_A_Result_Loaded_From_The_Store_Its_Estimate_Is_The_Runs()
    {
        // Arrange
        var text = string.Concat(Enumerable.Repeat($"\"{Data.Persistence.TextOutsideAscii}\" ", 100));
        var result = JsonSerializer.SerializeToElement(text, Json.GetTypeInfo<string>());
        var message = new ChatMessage(ChatRole.Tool, [new FunctionResultContent(Data.Tool.CallId, result)]);
        var entry = new ConversationEntry
        {
            Agent = "compacting_agent",
            Messages = [message],
            At = Time.GetUtcNow()
        };

        // Act
        var loaded = ConversationEntry.Deserialize(entry.Serialize());

        // Assert
        Tokens.Estimate(loaded.Messages, null).Should().Be(Tokens.Estimate([message], null));
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        _register(services);
    }

    private Task<ChatResponse> RunAsync(string input, CompactingAgent.Settings? settings = null) =>
        Host<CompactingAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(input, settings: settings, ct: Ct);

    private Task<ChatResponse> RunAsync(string input, CompactingAgent.Settings settings, ModelPolicy policy) =>
        Host<CompactingAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .WithModelPolicy(policy)
            .RunAsync(input, settings: settings, ct: Ct);

    private HttpRequestException Unavailable() =>
        new(Data.Agent.Reason, null, HttpStatusCode.ServiceUnavailable);

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

    // A model call's report of the tokens it took
    private ChatResponseUpdate Reported() =>
        new(ChatRole.Assistant, [new UsageContent(new UsageDetails { InputTokenCount = Data.Agent.InputTokens })]);

    // What compaction estimates a request at, before any report corrects it
    private int Estimate(int index)
    {
        var request = ChatClient.Assert.Request(index);
        return Tokens.Estimate(request.Messages, request.Options);
    }
}

#pragma warning restore TYPESHIFT001
