#pragma warning disable TYPESHIFT001 // a run under a model policy records its retries and fallbacks
using System.Net;
using System.Diagnostics;
using System.Text.Json;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Approval.Support;
using Typeshift.Tests.Compaction.Support;
using Typeshift.Tests.Conversations.Support;
using Typeshift.Tests.Hosting.Support;
using Typeshift.Tests.MultiAgent.Support;
using Typeshift.Tests.Telemetry.Support;
using Typeshift.Tests.Tools.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Telemetry;

public sealed class TelemetryTests : AgentTestBase
{
    // The span of the request a run starts in, as ASP.NET Core opens one
    private const string RequestSpan = "POST /chats";

    // What a tool span carries only with message capture
    private static readonly string[] ToolContent = ["gen_ai.tool.call.arguments", "gen_ai.tool.call.result"];

    // A listener of the test's own, from its constructor to its end; tests running alongside record into it too, so
    // each test finds its run by its conversation
    private RecordedActivities Activities { get; } = new();

    [Fact]
    public async Task Given_A_Run_It_Is_An_Invoke_Agent_Span_Around_Its_Chat_And_Tool_Spans()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync();

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        run.DisplayName.Should().Be("invoke_agent probe_agent");
        Activities.Inside(run).Select(activity => activity.DisplayName)
            .Should()
            .Equal($"chat {ProbeAgent.Model}", "execute_tool probe", $"chat {ProbeAgent.Model}");
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Every_Span_Hangs_Under_The_Run()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunStreamingAsync();

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        Activities.Under(run).Select(activity => activity.DisplayName)
            .Should()
            .Equal($"chat {ProbeAgent.Model}", "execute_tool probe", $"chat {ProbeAgent.Model}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Handoff_The_Span_Of_The_Agent_Handed_To_Hangs_Under_The_Handing_Agents(bool streaming)
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer));

        var front = Host<FrontAgent>().WithConversationId(Data.Agent.ConversationId);

        // Act
        if (streaming) await front.RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct);
        else await front.RunAsync(Data.Tool.Question, ct: Ct);

        var handing = Activities.Runs(Data.Agent.ConversationId)
            .Single(run => run.DisplayName == "invoke_agent front_agent");

        // Assert
        Activities.Under(handing).Select(activity => activity.DisplayName).Should().Contain("invoke_agent back_agent");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Later_Run_Of_A_Handed_Off_Conversation_Its_One_Span_Is_The_Answering_Agents(
        bool streaming)
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason));

        var front = Host<FrontAgent>().WithConversationId(Data.Agent.ConversationId);
        await front.RunAsync(Data.Tool.Question, ct: Ct);
        var before = Activities.Runs(Data.Agent.ConversationId).Count;

        // Act
        if (streaming) await front.RunStreamingAsync(Data.Agent.FollowUp, ct: Ct).ToListAsync(Ct);
        else await front.RunAsync(Data.Agent.FollowUp, ct: Ct);

        var later = Activities.Runs(Data.Agent.ConversationId).Skip(before);

        // Assert
        later.Select(run => run.DisplayName).Should().Equal("invoke_agent back_agent");
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Inside_A_Request_Span_Every_Span_Hangs_Under_The_Run()
    {
        // Arrange
        using var request = new Activity(RequestSpan).Start();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunStreamingAsync();

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        Activities.Under(run).Select(activity => activity.DisplayName)
            .Should()
            .Equal($"chat {ProbeAgent.Model}", "execute_tool probe", $"chat {ProbeAgent.Model}");
    }

    [Fact]
    public async Task Given_A_Typed_Streaming_Run_Every_Span_Hangs_Under_The_Run()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(JsonSerializer.Serialize(new ProbeSettings(), Json.GetTypeInfo<ProbeSettings>())));

        // Act
        await RunTypedStreamingAsync();

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        Activities.Under(run).Select(activity => activity.DisplayName)
            .Should()
            .Equal($"chat {ProbeAgent.Model}", "execute_tool probe", $"chat {ProbeAgent.Model}");
    }

    [Fact]
    public async Task Given_A_Typed_Streaming_Run_Inside_A_Request_Span_Every_Span_Hangs_Under_The_Run()
    {
        // Arrange
        using var request = new Activity(RequestSpan).Start();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(JsonSerializer.Serialize(new ProbeSettings(), Json.GetTypeInfo<ProbeSettings>())));

        // Act
        await RunTypedStreamingAsync();

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        Activities.Under(run).Select(activity => activity.DisplayName)
            .Should()
            .Equal($"chat {ProbeAgent.Model}", "execute_tool probe", $"chat {ProbeAgent.Model}");
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Inside_A_Request_Span_The_Request_Stays_Current_Between_Its_Updates()
    {
        // Arrange
        using var request = new Activity(RequestSpan).Start();
        var current = new List<Activity?>();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        var updates = Host<ProbeAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(Data.Tool.Question, ct: Ct);

        // Act
        await foreach (var _ in updates)
        {
            current.Add(Activity.Current);
        }

        // Assert
        current.Distinct().Should().Equal(request);
    }

    [Fact]
    public async Task Given_A_Run_Its_Span_Carries_The_Token_Usage()
    {
        // Arrange
        var usage = new UsageDetails
        {
            InputTokenCount = Data.Agent.InputTokens,
            OutputTokenCount = Data.Agent.OutputTokens
        };
        ChatClient.Reply(
            Text(Data.Tool.Answer),
            new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(usage)]));

        // Act
        await RunAsync();

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        run.GetTagItem("gen_ai.usage.input_tokens").Should().Be(Data.Agent.InputTokens);
        run.GetTagItem("gen_ai.usage.output_tokens").Should().Be(Data.Agent.OutputTokens);
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Its_Span_Carries_The_Token_Usage()
    {
        // Arrange
        var usage = new UsageDetails
        {
            InputTokenCount = Data.Agent.InputTokens,
            OutputTokenCount = Data.Agent.OutputTokens
        };
        ChatClient.Reply(
            Text(Data.Tool.Answer),
            new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(usage)]));

        // Act
        await RunStreamingAsync();

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        run.GetTagItem("gen_ai.usage.input_tokens").Should().Be(Data.Agent.InputTokens);
        run.GetTagItem("gen_ai.usage.output_tokens").Should().Be(Data.Agent.OutputTokens);
    }

    [Fact]
    public async Task Given_Message_Capture_A_Run_Tags_The_Tool_Span_With_Arguments_And_Result()
    {
        // Arrange
        await CaptureMessagesAsync(true);

        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync();

        // Assert
        ToolSpan().TagObjects.Select(tag => tag.Key).Should().Contain(ToolContent);
    }

    [Fact]
    public async Task Given_Message_Capture_A_Streaming_Run_Tags_The_Tool_Span_With_Arguments_And_Result()
    {
        // Arrange
        await CaptureMessagesAsync(true);

        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunStreamingAsync();

        // Assert
        ToolSpan().TagObjects.Select(tag => tag.Key).Should().Contain(ToolContent);
    }

    [Fact]
    public async Task Given_No_Message_Capture_A_Run_Leaves_Arguments_And_Result_Off_The_Tool_Span()
    {
        // Arrange
        await CaptureMessagesAsync(false);

        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync();

        // Assert
        ToolSpan().TagObjects.Select(tag => tag.Key).Should().NotContain(ToolContent);
    }

    [Fact]
    public async Task Given_A_Failing_Run_Its_Span_Carries_The_Error()
    {
        // Arrange
        ChatClient.Reply(new InvalidOperationException(Data.Agent.Reason));

        // Act
        var run = () => RunAsync();

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage(Data.Agent.Reason);
        var span = Activities.Run(Data.Agent.ConversationId);
        span.Status.Should().Be(ActivityStatusCode.Error);
        span.GetTagItem("error.type").Should().Be(typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public async Task Given_A_Retry_Of_The_Model_Policy_The_Runs_Span_Carries_It_As_An_Event()
    {
        // Arrange
        ChatClient.Reply(Unavailable()).Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(ModelPolicy.Handle(ModelFailure.Unavailable).Retry(1));

        // Assert
        Activities.Run(Data.Agent.ConversationId).Events.Single(@event => @event.Name == "typeshift.model.retry").Tags
            .Should()
            .BeEquivalentTo(new Dictionary<string, object?>
            {
                ["typeshift.model.failure"] = "Unavailable",
                ["error.type"] = typeof(HttpRequestException).FullName,
                ["typeshift.model.from.provider"] = ProbeAgent.ModelProvider,
                ["typeshift.model.from.model"] = ProbeAgent.Model,
                ["typeshift.model.to.provider"] = ProbeAgent.ModelProvider,
                ["typeshift.model.to.model"] = ProbeAgent.Model,
                ["typeshift.model.retries"] = 1
            });
    }

    [Fact]
    public async Task Given_A_Fallback_Of_The_Model_Policy_The_Runs_Span_Carries_It_As_An_Event()
    {
        // Arrange
        ChatClient.Reply(Unavailable()).Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(ModelPolicy.Handle(ModelFailure.Transient).FallbackTo(Data.Agent.Provider, Data.Agent.Model));

        // Assert
        Activities.Run(Data.Agent.ConversationId).Events.Single(@event => @event.Name == "typeshift.model.fallback")
            .Tags
            .Should()
            .BeEquivalentTo(new Dictionary<string, object?>
            {
                ["typeshift.model.failure"] = "Unavailable",
                ["error.type"] = typeof(HttpRequestException).FullName,
                ["typeshift.model.from.provider"] = ProbeAgent.ModelProvider,
                ["typeshift.model.from.model"] = ProbeAgent.Model,
                ["typeshift.model.to.provider"] = Data.Agent.Provider,
                ["typeshift.model.to.model"] = Data.Agent.Model
            });
    }

    [Fact]
    public async Task Given_A_Fallback_Each_Attempt_Has_A_Chat_Span_Of_The_Model_It_Called()
    {
        // Arrange
        ChatClient.Reply(Unavailable()).Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(ModelPolicy.Handle(ModelFailure.Transient).FallbackToModel(Data.Agent.Model));

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        Activities.Inside(run).Select(activity => activity.DisplayName)
            .Should()
            .Equal($"chat {ProbeAgent.Model}", $"chat {Data.Agent.Model}");
    }

    [Fact]
    public async Task Given_A_Failed_Run_Its_Span_Carries_The_Exception()
    {
        // Arrange
        ChatClient.Reply(new InvalidOperationException(Data.Agent.Reason));

        // Act
        await Record.ExceptionAsync(() => RunAsync());

        // Assert
        Activities.Run(Data.Agent.ConversationId).Events
            .Where(logged => logged.Name == "exception")
            .SelectMany(logged => logged.Tags)
            .Should()
            .Contain(tag => tag.Key == "exception.message" && (string?)tag.Value == Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Tool_That_Throws_Its_Span_Carries_The_Exception_The_Model_Does_Not_Read()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "crash", ("message", Data.Agent.Reason)))
            .Reply(Text(Data.Tool.Answer));

        var host = Host<FailingAgent>().WithConversationId(Data.Agent.ConversationId);

        // Act
        await host.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        var span = Activities.Under(Activities.Run(Data.Agent.ConversationId))
            .Single(activity => activity.DisplayName == "execute_tool crash");

        span.GetTagItem("error.type").Should().Be(typeof(InvalidOperationException).FullName);
        span.StatusDescription.Should().Be(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Run_That_Waits_For_A_Decision_Its_Span_Carries_No_Error_And_No_Tool_Span()
    {
        // Arrange
        ChatClient.Reply(Call(Data.Tool.CallId, "close_ticket", ("number", Data.Agent.Ticket.Default)));

        // Act
        await Host<GuardedAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        run.Status.Should().NotBe(ActivityStatusCode.Error);
        Activities.Inside(run).Select(activity => activity.DisplayName).Should().Equal("chat guarded-model");
    }

    [Fact]
    public async Task Given_A_Streaming_Raw_Call_Whose_Conversation_No_Store_Keeps_Its_Span_Carries_The_Error()
    {
        // Arrange
        var host = Host<ProbeAgent>();
        var options = await host.ResolveOptionsAsync(ct: Ct);
        options.ConversationId = Data.Persistence.ChatName;
        using var client = await host.CreateChatClientAsync(options, Ct);

        // Act
        var run = () => client.GetStreamingResponseAsync(Data.Tool.Question, options, Ct).ToListAsync(Ct).AsTask();

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("No IConversationStore<string>*");
        Activities.Run(Data.Persistence.ChatName).Status.Should().Be(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task Given_A_Store_That_Fails_To_Append_The_Runs_Span_Records_The_Exception()
    {
        // Arrange
        var services = new ServiceCollection();
#pragma warning disable TSH036 // the store fails every append, and the run never asks
        services.AddTypeshift().WithStore<FailingAppendStore>().WithScriptedProviders();
#pragma warning restore TSH036
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Record.ExceptionAsync(() => scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct));

        // Assert
        Activities.Run(Data.Agent.ConversationId).Events.Select(@event => @event.Name).Should().Contain("exception");
    }

    [Fact]
    public async Task Given_The_Continue_Option_A_Run_Whose_Store_Fails_To_Append_Is_An_Error_On_Its_Span()
    {
        // Arrange
        var services = new ServiceCollection();

#pragma warning disable TSH036 // the store fails every append, and the run never asks
        services.AddTypeshift()
            .WithStore<FailingAppendStore>()
            .WithScriptedProviders()
            .WithOptions(options => options.StoreFailure = StoreFailure.ContinueRun);
#pragma warning restore TSH036

        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Activities.Run(Data.Agent.ConversationId).Status.Should().Be(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task Given_A_Summary_Model_That_Fails_The_Runs_Span_Records_The_Exception_And_No_Error()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "read", ("length", Page)))
            .Reply(new HttpRequestException(Data.Agent.Instructions))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<CompactingAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(
                Data.Tool.Question,
                settings: new() { MaxInputTokens = 1_000, SummaryModel = Data.Agent.Model },
                ct: Ct);

        // Assert
        Activities.Run(Data.Agent.ConversationId).Events.Select(@event => @event.Name).Should().Contain("exception");
        Activities.Run(Data.Agent.ConversationId).Status.Should().NotBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task Given_Two_Runs_On_One_Provider_Client_Their_Chat_Spans_Come_From_One_Source()
    {
        // Arrange
        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync();
        await RunAsync();

        // Assert
        Activities.Runs(Data.Agent.ConversationId)
            .SelectMany(Activities.Inside)
            .Select(activity => activity.Source)
            .Distinct()
            .Should()
            .ContainSingle();
    }

    [Fact]
    public async Task Given_Two_Runs_On_A_Provider_Client_Per_Call_Their_Chat_Spans_Come_From_One_Source()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift()
            .WithStore<InMemoryTypeshiftStore<Guid>>()
            .WithClientOfEveryProvider((_, _) => new ClientPerCall(ChatClient));
        services.AddSingleton(ChatClient);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId);

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await host.RunAsync(Data.Tool.Question, ct: Ct);
        await host.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Activities.Runs(Data.Agent.ConversationId)
            .SelectMany(Activities.Inside)
            .Select(activity => activity.Source)
            .Distinct()
            .Should()
            .ContainSingle();
    }

    [Fact]
    public async Task Given_Runs_Naming_Many_Models_On_One_Provider_They_Share_One_Instrumented_Client()
    {
        // Arrange
        var backend = Data.Telemetry.Backend;

        var clients = Data.Telemetry.Models
            .Select(model => new ChatClientMetadata(backend.ProviderName, backend.ProviderUri, model));

        await using var provider = ServicesOf([.. clients]);
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId);

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        // Act
        foreach (var model in Data.Telemetry.Models)
        {
            await host.RunAsync(Data.Tool.Question, settings: new() { Model = model }, ct: Ct);
        }

        // Assert
        Activities.Runs(Data.Agent.ConversationId)
            .SelectMany(Activities.Inside)
            .Select(activity => activity.Source)
            .Distinct()
            .Should()
            .ContainSingle();
    }

    [Fact]
    public async Task Given_Two_Clients_Of_Different_Metadata_Under_One_Provider_Each_Span_Names_Its_Own_Backend()
    {
        // Arrange
        var first = Data.Telemetry.Backend;
        var second = Data.Telemetry.SecondBackend;
        await using var provider = ServicesOf(first, second);
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId);

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await host.RunAsync(Data.Tool.Question, ct: Ct);
        await host.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Activities.Runs(Data.Agent.ConversationId)
            .SelectMany(Activities.Inside)
            .Select(activity => (activity.GetTagItem("gen_ai.provider.name"), activity.GetTagItem("server.address")))
            .Should()
            .Equal((first.ProviderName, first.ProviderUri!.Host), (second.ProviderName, second.ProviderUri!.Host));
    }

    [Fact]
    public async Task Given_A_Run_Naming_No_Model_Its_Chat_Span_Names_The_Clients_Own_Model()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<InMemoryTypeshiftStore<Guid>>();
        services.AddChatClient(new BackendClient(ChatClient, new ChatClientMetadata(defaultModelId: Data.Agent.Model)));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await scope.ServiceProvider.GetRequiredService<AgentHost<SchemaAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        Activities.Inside(run).Select(activity => activity.DisplayName).Should().Equal($"chat {Data.Agent.Model}");
    }

    [Fact]
    public async Task Given_A_Run_That_Names_A_Provider_Its_Span_Carries_The_Provider()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync();

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        run.GetTagItem("typeshift.provider").Should().Be(ProbeAgent.ModelProvider);
    }

    [Fact]
    public async Task Given_Settings_That_Name_Another_Provider_The_Runs_Span_Carries_That_Provider()
    {
        // Arrange
        var settings = new ProbeAgent.Settings { Provider = Data.Agent.Provider };
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, settings, Ct);

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        run.GetTagItem("typeshift.provider").Should().Be(Data.Agent.Provider);
    }

    [Fact]
    public async Task Given_A_Run_Handed_A_Document_Its_Span_Carries_Its_Name_And_Not_Its_Key()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<InMemoryTypeshiftStore<Guid>>().WithProviderConnector<CountingConnector>();
        services.AddSingleton(ChatClient).AddSingleton(new Connections());

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var document = new ChatProvider
        {
            Kind = "scripted",
            Name = Data.Agent.Instructions,
            ApiKey = Data.Agent.Model
        };
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await scope.ServiceProvider.GetRequiredService<AgentHost<SchemaAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .WithProvider(document)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        run.GetTagItem("typeshift.provider").Should().Be(Data.Agent.Instructions);
    }

    [Fact]
    public async Task Given_A_Run_On_The_Default_Client_Its_Span_Carries_No_Provider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<InMemoryTypeshiftStore<Guid>>();
        services.AddChatClient(ChatClient);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await scope.ServiceProvider.GetRequiredService<AgentHost<SchemaAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        run.GetTagItem("typeshift.provider").Should().BeNull();
    }

    [Fact]
    public async Task Given_A_Handoff_To_An_Agent_Of_Another_Provider_Each_Agents_Span_Carries_Its_Own_Provider()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, BackOfficeAgent.Handoff))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<FrontDeskAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Activities.Runs(Data.Agent.ConversationId)
            .Select(run => $"{run.GetTagItem("gen_ai.agent.name")} on {run.GetTagItem("typeshift.provider")}")
            .Should()
            .BeEquivalentTo(
                $"{FrontDeskAgent.AgentName} on {FrontDeskAgent.ModelProvider}",
                $"{BackOfficeAgent.AgentName} on {BackOfficeAgent.ModelProvider}");
    }

    [Fact]
    public async Task Given_A_Run_Passed_On_To_The_Agent_That_Answered_Last_Its_Span_Carries_That_Agents_Provider()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, BackOfficeAgent.Handoff))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason));

        var front = Host<FrontDeskAgent>().WithConversationId(Data.Agent.ConversationId);
        await front.RunAsync(Data.Tool.Question, ct: Ct);
        var before = Activities.Runs(Data.Agent.ConversationId).Count;

        // Act
        await front.RunAsync(Data.Agent.FollowUp, ct: Ct);
        var later = Activities.Runs(Data.Agent.ConversationId).Skip(before);

        // Assert
        later.Select(run => run.GetTagItem("typeshift.provider")).Should().Equal(BackOfficeAgent.ModelProvider);
    }

    [Fact]
    public async Task Given_A_Container_Disposed_The_Meters_Of_The_Backends_Its_Runs_Called_End()
    {
        // Arrange
        using var meters = new RecordedMeters(Data.Telemetry.Backend.ProviderUri!.Host);
        await using var provider = ServicesOf(Data.Telemetry.Backend);
        ChatClient.Reply(Text(Data.Tool.Answer));

        await using (var scope = provider.CreateAsyncScope())
        {
            var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>();
            await host.RunAsync(Data.Tool.Question, ct: Ct);
        }

        // Act
        await provider.DisposeAsync();

        // Assert
        meters.Ended.Should().Contain("gen_ai.client.operation.duration");
    }

    protected override ValueTask DisposeCoreAsync()
    {
        Activities.Dispose();
        return ValueTask.CompletedTask;
    }

    private Task<ChatResponse> RunAsync() =>
        Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);

    private Task<ChatResponse> RunAsync(ModelPolicy policy) =>
        Host<ProbeAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .WithModelPolicy(policy)
            .RunAsync(Data.Tool.Question, ct: Ct);

    private HttpRequestException Unavailable() =>
        new(Data.Agent.Reason, null, HttpStatusCode.ServiceUnavailable);

    // A container that serves each run from a client of the next backend, as a client per customer or per model would
    private ServiceProvider ServicesOf(params ChatClientMetadata[] backends)
    {
        var queue = new Backends(backends);
        var services = new ServiceCollection();
        services.AddTypeshift()
            .WithStore<InMemoryTypeshiftStore<Guid>>()
            .WithClientOfEveryProvider((_, _) => new BackendClient(ChatClient, queue.Dequeue()));
        services.AddSingleton(ChatClient);

        return services.BuildServiceProvider();
    }

    private ValueTask<List<ChatResponseUpdate>> RunStreamingAsync() =>
        Host<ProbeAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(Data.Tool.Question, ct: Ct)
            .ToListAsync(Ct);

    private ValueTask<List<ChatResponseUpdate>> RunTypedStreamingAsync() =>
        Host<ProbeAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync<ProbeSettings>(Data.Tool.Question, ct: Ct)
            .ToListAsync(Ct);

    // Capture as OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT sets it for a process, here for this test's host
    private async Task CaptureMessagesAsync(bool capture)
    {
        var host = Host<ProbeAgent>();
        var options = await host.ResolveOptionsAsync(ct: Ct);
        using var client = await host.CreateChatClientAsync(options, Ct);

        client.GetService<OpenTelemetryChatClient>()!.EnableSensitiveData = capture;
    }

    private Activity ToolSpan() =>
        Activities.Under(Activities.Run(Data.Agent.ConversationId))
            .Single(activity => activity.DisplayName == "execute_tool probe");
}
