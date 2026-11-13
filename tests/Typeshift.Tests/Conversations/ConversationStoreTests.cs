using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Compaction.Support;
using Typeshift.Tests.Conversations.Support;
using Typeshift.Tests.Persistence.Support;
using Typeshift.Tests.Testing.Fakes;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Typeshift.Tests.Conversations;

public class ConversationStoreTests : AgentTestBase
{
    [Fact]
    public async Task Given_A_Summary_The_Store_Loads_It_And_The_Entries_After_It()
    {
        // Arrange
        ITypeshiftStore<Guid> store = new InMemoryTypeshiftStore<Guid>();
        var conversation = Data.Agent.ConversationId;

        ConversationEntry[] entries =
        [
            new("probe_agent", [new ChatMessage(ChatRole.User, Data.Tool.Question)], Time.GetUtcNow()),
            new("probe_agent", [new ChatMessage(ChatRole.User, Data.Agent.Reason)], Time.GetUtcNow())
            {
                IsSummary = true
            },
            new("probe_agent", [new ChatMessage(ChatRole.User, Data.Agent.FollowUp)], Time.GetUtcNow())
        ];

        foreach (var entry in entries)
        {
            await store.AppendConversationAsync(conversation, entry, Ct);
        }

        // Act
        var loaded = await store.LoadConversationSinceCompactionAsync(conversation, Ct);

        // Assert
        loaded.Select(entry => entry.Messages[0].Text).Should().Equal(Data.Agent.Reason, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_A_Summary_The_Next_Run_Never_Loads_The_Whole_Conversation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<CountingLoadStore>().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<CompactingAgent>>()
            .WithConversationId(Data.Agent.ConversationId);
        var settings = new CompactingAgent.Settings { MaxInputTokens = 1_000 };

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        await host.RunAsync(new string('x', Page), ct: Ct);
        await host.RunAsync(Data.Agent.FollowUp, settings: settings, ct: Ct);

        // Act
        await host.RunAsync(Data.Agent.Steering, ct: Ct);

        // Assert
        provider.GetRequiredService<CountingLoadStore>().WholeLoads.Should().Be(0);
    }

    [Fact]
    public async Task Given_A_Run_Loading_Its_Conversation_A_Second_Run_Of_It_Fails_Once_The_First_Began()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<GatedLoadStore>().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId);
        var store = provider.GetRequiredService<GatedLoadStore>();

        var answering = new TaskCompletionSource();
        ChatClient.Reply(answering, Text(Data.Tool.Answer));

        store.HoldNextLoad();
        var first = host.RunAsync(Data.Tool.Question, ct: Ct);
        await store.Loading.Task;

        // Act
        var second = host.RunAsync(Data.Agent.FollowUp, ct: Ct);
        store.Gate.SetResult();

        // Assert
        await FluentActions.Awaiting(() => second).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TrySteer*");
        answering.SetResult();
        await first;
    }

    [Fact]
    public async Task Given_A_Run_Loading_Its_Conversation_That_Fails_To_Begin_A_Second_Run_Of_It_Runs()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<GatedLoadStore>().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId);
        var store = provider.GetRequiredService<GatedLoadStore>();

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason));

        await host.RunAsync(Data.Tool.Question, ct: Ct);

        store.HoldNextLoad();
        var resume = host.ResumeAsync(ct: Ct);
        await store.Loading.Task;

        // Act
        var second = host.RunAsync(Data.Agent.FollowUp, ct: Ct);
        store.Gate.SetResult();

        // Assert
        await FluentActions.Awaiting(() => resume).Should().ThrowAsync<InvalidOperationException>();
        (await second).Text.Should().Be(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Store_That_Fails_To_Append_The_Run_Still_Answers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<FailingAppendStore>().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Content_Of_A_Type_The_Contract_Does_Not_Know_The_Rest_Of_The_Step_Is_Stored()
    {
        // Arrange
        ChatClient.Reply(Answer(new TextContent(Data.Tool.Answer), new VendorContent()));

        // Act
        await Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);
        var entries = await EntriesAsync(Data.Agent.ConversationId);

        // Assert
        entries.SelectMany(entry => entry.Messages).Select(message => message.Text).Should().Contain(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_Two_Steps_With_A_Content_The_Contract_Does_Not_Know_The_Run_Warns_Once_Naming_Its_Type()
    {
        // Arrange
        var logs = new WarningLog();
        var services = new ServiceCollection();
        services.AddTypeshift()
            .WithStore<InMemoryTypeshiftStore<Guid>>()
            .WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models).AddSingleton<ILoggerFactory>(logs);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var call = Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query));

        ChatClient
            .Reply(Answer([.. call.Contents, new VendorContent()]))
            .Reply(Answer(new TextContent(Data.Tool.Answer), new VendorContent()));

        // Act
        await scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        logs.Warnings.Should().ContainSingle(warning => warning.Contains(typeof(VendorContent).FullName!));
    }

    [Fact]
    public async Task Given_A_Conversation_With_Contents_It_Cannot_Read_The_Next_Run_Sends_The_Others()
    {
        // Arrange
        var store = SeededWithContentsOfAnotherVersion();
        await using var provider = Provider(store, new WarningLog());
        await using var scope = provider.CreateAsyncScope();
        ChatClient.Reply(Text(Data.Agent.Reason));

        // Act
        await scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        ChatClient.Requests[0].Select(message => message.Text)
            .Should()
            .Equal(Data.Tool.Question, Data.Tool.Answer, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_A_Conversation_With_Contents_It_Cannot_Read_The_Next_Run_Warns_Once_With_Their_Count()
    {
        // Arrange
        var logs = new WarningLog();
        var store = SeededWithContentsOfAnotherVersion();
        await using var provider = Provider(store, logs);
        await using var scope = provider.CreateAsyncScope();
        ChatClient.Reply(Text(Data.Agent.Reason));

        // Act
        await scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        logs.Warnings.Should().ContainSingle(warning => warning.Contains("holds 2 contents"));
    }

    [Fact]
    public async Task Given_No_Store_A_Conversation_Is_Kept_In_Memory_And_Its_Next_Run_Resumes_It()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason));

        await using (var first = provider.CreateAsyncScope())
        {
            await first.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
                .WithConversationId(Data.Agent.ConversationId)
                .RunAsync(Data.Tool.Question, ct: Ct);
        }

        await using var second = provider.CreateAsyncScope();

        // Act
        await second.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        ChatClient.Requests[1].Select(message => message.Text).Should().Contain(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_No_Store_The_First_Run_That_Stores_Warns_Once_That_It_Keeps_Them_In_Memory()
    {
        // Arrange
        var logs = new WarningLog();
        var services = new ServiceCollection();
        services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models).AddSingleton<ILoggerFactory>(logs);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId);

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason));

        // Act
        await host.RunAsync(Data.Tool.Question, ct: Ct);
        await host.RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        logs.Warnings.Should().ContainSingle();
        logs.Warnings[0].Should().Contain("WithStore");
        logs.Warnings[0].Should().Contain("WithFileStore");
    }

    [Fact]
    public async Task Given_A_Store_Of_Settings_Alone_A_Run_With_A_Conversation_Fails_Naming_IConversationStore()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<SettingsOnlyStore>().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        // Act
        var run = () => scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*IConversationStore*");
    }

    [Fact]
    public async Task Given_A_Store_Of_Settings_And_One_Of_Conversations_A_Run_Uses_Each()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddTypeshift()
            .WithStore<SettingsOnlyStore>()
            .WithStore<CountingLoadStore>()
            .WithChatClientProvider<ScriptedChatClientProvider>();

        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var conversations = provider.GetRequiredService<CountingLoadStore>();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithSettingsId(Data.Persistence.SettingsId);

        var stored = new ProbeAgent.Settings
        {
            Provider = Data.Agent.Provider,
            Model = Data.Agent.Model
        };

        ChatClient.Reply(Text(Data.Tool.Answer));
        await host.SaveSettingsAsync(stored, 0, Ct);

        // Act
        await host.WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        var entries = await conversations.LoadConversationAsync(Data.Agent.ConversationId, Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(Data.Agent.Provider, Data.Agent.Model));

        entries.Should().NotBeEmpty();
    }

    [Fact]
    public void Given_Realtime_Options_They_Replace_The_Defaults()
    {
        // Arrange
        var options = new RealtimeTrackingOptions { MaxPendingUpdates = 1024 };
        var services = new ServiceCollection();

        // Act
        services.AddTypeshift().WithRealtimeTracking(options);

        // Assert
        using var provider = services.BuildServiceProvider();
        provider.GetServices<RealtimeTrackingOptions>().Should().Equal(options);
    }

    [Fact]
    public async Task Given_No_WithRealtimeTracking_A_Watcher_Receives_The_Updates_Of_A_Streaming_Run()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var services = new ServiceCollection();
        services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var watch = provider.GetRequiredService<ConversationTracker<Guid>>()
            .WatchAsync(Data.Agent.ConversationId, watching.Token)
            .GetAsyncEnumerator(Ct);

        var next = watch.MoveNextAsync();

        // Act
        await scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(Data.Tool.Question, ct: Ct)
            .ToListAsync(Ct);

        // Assert
        (await next).Should().BeTrue();
        watch.Current.Text.Should().Be(Data.Tool.Answer);
    }

    private static ChatResponseUpdate Answer(params AIContent[] contents) => new(ChatRole.Assistant, contents);

    // A run as an earlier version of Microsoft.Extensions.AI stored it: its answer holds two contents under type
    // names this version does not know
    private StoredJsonStore SeededWithContentsOfAnotherVersion()
    {
        var store = new StoredJsonStore();

        string[] entries =
        [
            $$"""
            {"agent":"probe_agent","at":"2026-10-11T08:00:00+00:00","messages":[{"role":"user","contents":[
              {"$type":"text","text":"{{Data.Tool.Question}}"}]}]}
            """,
            $$"""
            {"agent":"probe_agent","at":"2026-10-11T08:00:01+00:00","messages":[{"role":"assistant","contents":[
              {"$type":"text","text":"{{Data.Tool.Answer}}"},
              {"$type":"functionApprovalRequest","id":"r1","functionCall":{"callId":"c1","name":"probe"} },
              {"$type":"vendorThought","text":"..."}]}]}
            """,
            """{"agent":"probe_agent","at":"2026-10-11T08:00:02+00:00","endsRun":true}"""
        ];

        foreach (var entry in entries) store.Add(Data.Agent.ConversationId, entry);
        return store;
    }

    private ServiceProvider Provider(StoredJsonStore store, WarningLog logs)
    {
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore(store).WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models).AddSingleton<ILoggerFactory>(logs);

        return services.BuildServiceProvider();
    }
}
