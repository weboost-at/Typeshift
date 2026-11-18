using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Approval.Support;
using Typeshift.Tests.Compaction.Support;
using Typeshift.Tests.Conversations.Support;
using Typeshift.Tests.Decisions.Support;
using Typeshift.Tests.Persistence.Support;
using Typeshift.Tests.Testing.Fakes;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Stores that keep conversations alone are what these tests register, for runs that ask nobody
#pragma warning disable TSH036

namespace Typeshift.Tests.Conversations;

public class ConversationStoreTests : AgentTestBase
{
    // The containers of other instances of the app, with their scopes, disposed after the test
    private readonly List<IAsyncDisposable> _instances = [];

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
        await store.Loading.Task.WaitAsync(Ct);

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
        await store.Loading.Task.WaitAsync(Ct);

        // Act
        var second = host.RunAsync(Data.Agent.FollowUp, ct: Ct);
        store.Gate.SetResult();

        // Assert
        await FluentActions.Awaiting(() => resume).Should().ThrowAsync<InvalidOperationException>();
        (await second).Text.Should().Be(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Store_Whose_Load_Failed_The_Next_Run_Of_The_Conversation_Runs()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<FailingLoadStore>().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId);

        ChatClient.Reply(Text(Data.Tool.Answer));
        var failed = await Record.ExceptionAsync(() => host.RunAsync(Data.Tool.Question, ct: Ct));

        // Act
        var response = await host.RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        failed.Should().BeOfType<IOException>();
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Run_Waiting_For_A_Conversation_That_Loads_Cancelling_It_Ends_The_Wait()
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

        ChatClient.Reply(Text(Data.Tool.Answer));

        store.HoldNextLoad();
        var first = host.RunAsync(Data.Tool.Question, ct: Ct);
        await store.Loading.Task.WaitAsync(Ct);

        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var second = host.RunAsync(Data.Agent.FollowUp, ct: cancel.Token);

        // Act
        await cancel.CancelAsync();
        var waited = await Record.ExceptionAsync(() => second);
        var loading = !first.IsCompleted;

        // Assert
        waited.Should().BeAssignableTo<OperationCanceledException>();
        loading.Should().BeTrue();
        store.Gate.SetResult();
        await first;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Store_That_Fails_To_Append_The_Run_Throws_Carrying_The_Answer(bool streaming)
    {
        // Arrange
        await using var provider = FailingAppends(StoreFailure.FailRun);
        await using var scope = provider.CreateAsyncScope();
        var chat = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId);

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var failed = await Record.ExceptionAsync(async () =>
        {
            if (streaming) await chat.RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct);
            else await chat.RunAsync(Data.Tool.Question, ct: Ct);
        });

        // Assert
        failed.Should().BeOfType<ConversationStoreException>();
        ((ConversationStoreException)failed!).Response.Text.Should().Be(Data.Tool.Answer);
        failed.InnerException.Should().BeOfType<IOException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_The_Continue_Option_The_Run_Answers_And_The_Response_Says_The_Conversation_Is_Incomplete(
        bool streaming)
    {
        // Arrange
        await using var provider = FailingAppends(StoreFailure.ContinueRun);
        await using var scope = provider.CreateAsyncScope();
        var chat = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId);

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = streaming
            ? (await chat.RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct)).ToChatResponse()
            : await chat.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
        response.ConversationIncomplete.Should().BeTrue();
    }

    [Fact]
    public async Task Given_A_Store_That_Appends_Every_Step_The_Response_Says_Nothing_Is_Missing()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ProbeAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.ConversationIncomplete.Should().BeFalse();
    }

    [Fact]
    public async Task Given_A_Handoff_Whose_Input_Was_Not_Stored_Under_The_Continue_Option_The_Response_Is_Incomplete()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddTypeshift()
            .WithStore<FlakyAppendStore>()
            .WithChatClientProvider<ScriptedChatClientProvider>()
            .WithOptions(options => options.StoreFailure = StoreFailure.ContinueRun);

        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        provider.GetRequiredService<FlakyAppendStore>().Failing = 1;

        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await scope.ServiceProvider.GetRequiredService<AgentHost<MultiAgent.Support.FrontAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.ConversationIncomplete.Should().BeTrue();
    }

    [Fact]
    public async Task Given_A_Store_That_Fails_To_Append_The_Failure_Is_Logged_As_An_Error()
    {
        // Arrange
        var log = new ErrorLog();
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<FailingAppendStore>().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models).AddSingleton<ILoggerFactory>(log);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Record.ExceptionAsync(() => scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct));

        // Assert
        log.Errors.Should().Contain(error => error.Exception is IOException);
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
    public async Task Given_A_Store_That_Failed_To_Append_A_Response_The_Next_Run_Leaves_Its_Tool_Result_Out()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<FlakyAppendStore>().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId);

        provider.GetRequiredService<FlakyAppendStore>().Failing = 2;

        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason));

        await Record.ExceptionAsync(() => host.RunAsync(Data.Tool.Question, ct: Ct));

        // Act
        await host.RunAsync(Data.Agent.FollowUp, ct: Ct);
        var sent = ChatClient.Requests[2].SelectMany(message => message.Contents).ToList();

        // Assert
        sent.OfType<FunctionResultContent>().Should().BeEmpty();
        sent.OfType<TextContent>().Select(text => text.Text).Should().Contain(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_Two_Containers_On_One_Conversation_The_Second_Writer_Is_Refused()
    {
        // Arrange
        var store = new InMemoryTypeshiftStore<Guid>();
        var hold = new ToolHold();
        var other = new ScriptedChatClient(Ct);

        ChatClient.Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)));
        other.Reply(Text(Data.Tool.Answer));

        var held = Instance(store, ChatClient, hold).RunAsync(Data.Tool.Question, ct: Ct);
        await hold.Running.Task.WaitAsync(Ct);
        await Instance(store, other).RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Act
        hold.Release.SetResult();

        // Assert
        await FluentActions.Awaiting(() => held).Should().ThrowAsync<ConversationBusyException>();
    }

    [Fact]
    public async Task Given_Two_Containers_On_One_Conversation_The_Refused_Run_Stores_Nothing_After_The_Other()
    {
        // Arrange
        var store = new InMemoryTypeshiftStore<Guid>();
        var hold = new ToolHold();
        var other = new ScriptedChatClient(Ct);

        ChatClient.Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)));
        other.Reply(Text(Data.Tool.Answer));

        var held = Instance(store, ChatClient, hold).RunAsync(Data.Tool.Question, ct: Ct);
        await hold.Running.Task.WaitAsync(Ct);
        await Instance(store, other).RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Act
        hold.Release.SetResult();
        await Record.ExceptionAsync(() => held);

        // Assert
        (await store.LoadConversationAsync(Data.Agent.ConversationId, Ct))
            .Select(entry => entry.Sequence)
            .Should()
            .Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task Given_Another_Container_Took_The_Number_Of_A_Runs_Input_The_Run_Calls_No_Model()
    {
        // Arrange
        var other = new ScriptedChatClient(Ct);

        var store = new GatedEndStore
        {
            Holds = entry => entry.Messages is [{ Text: var text }] && text == Data.Tool.Question
        };

        other.Reply(Text(Data.Tool.Answer));

        var held = Instance(store, ChatClient).RunAsync(Data.Tool.Question, ct: Ct);
        await store.Ending.Task.WaitAsync(Ct);
        await Instance(store, other).RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Act
        store.Gate.SetResult();
        var refused = await Record.ExceptionAsync(() => held);

        // Assert
        refused.Should().BeOfType<ConversationBusyException>();
        ChatClient.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Run_Whose_End_Alone_Another_Container_Took_The_Number_Of_It_Returns_Its_Answer()
    {
        // Arrange
        var store = new GatedEndStore();
        var other = new ScriptedChatClient(Ct);

        ChatClient.Reply(Text(Data.Tool.Answer));
        other.Reply(Text(Data.Agent.Reason));

        var ending = Instance(store, ChatClient).RunAsync(Data.Tool.Question, ct: Ct);
        await store.Ending.Task.WaitAsync(Ct);
        await Instance(store, other).RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Act
        store.Gate.SetResult();

        // Assert
        (await ending).Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Conversation_Stored_Before_Entries_Were_Numbered_Its_Next_Entry_Follows_Their_Count()
    {
        // Arrange
        var store = SeededWithContentsOfAnotherVersion();
        await using var provider = Provider(store, new WarningLog());
        await using var scope = provider.CreateAsyncScope();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        (await store.LoadConversationAsync(Data.Agent.ConversationId, Ct))[3].Sequence.Should().Be(4);
    }

    [Fact]
    public async Task Given_An_Earlier_Versions_Summary_After_Numbered_Entries_The_Next_Entry_Takes_A_New_Number()
    {
        // Arrange
        var store = new StoredJsonStore();

        string[] entries =
        [
            $$"""
            {"agent":"probe_agent","at":"2026-10-11T08:00:00+00:00","sequence":1,"messages":[{"role":"user",
              "contents":[{"$type":"text","text":"{{Data.Tool.Question}}"}]}]}
            """,
            """{"agent":"probe_agent","at":"2026-10-11T08:00:01+00:00","sequence":2,"endsRun":true}""",
            $$"""
            {"agent":"probe_agent","at":"2026-10-11T08:00:02+00:00","isSummary":true,"messages":[{"role":"user",
              "contents":[{"$type":"text","text":"{{Data.Agent.Reason}}"}]}]}
            """,
            """{"agent":"probe_agent","at":"2026-10-11T08:00:03+00:00","endsRun":true}"""
        ];

        foreach (var entry in entries) store.Add(Data.Agent.ConversationId, entry);

        await using var provider = Provider(store, new WarningLog());
        await using var scope = provider.CreateAsyncScope();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        (await store.LoadConversationAsync(Data.Agent.ConversationId, Ct))[4].Sequence.Should().Be(5);
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
    public async Task Given_No_Store_A_Decision_Is_Kept_In_Memory_And_Its_Approval_Runs_The_Call()
    {
        // Arrange
        var calls = new ToolCalls();
        var services = new ServiceCollection();
        services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models).AddSingleton(calls);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<GuardedAgent>>()
            .WithConversationId(Data.Agent.ConversationId);

        ChatClient
            .Reply(Call(Data.Tool.CallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Text(Data.Tool.Answer));

        var asked = await host.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await host.AnswerAsync([DecisionAnswer.Approve(asked.Decisions[0].Id)], ct: Ct);

        // Assert
        calls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_The_File_Store_Alone_A_Run_With_A_Conversation_Fails_Naming_What_Is_Registered()
    {
        // Arrange
        var folder = Path.Combine(Path.GetTempPath(), $"typeshift-{Guid.NewGuid():N}");
        var services = new ServiceCollection();

        services.AddTypeshift()
            .WithFileStore(store => store.Folder = folder)
            .WithChatClientProvider<ScriptedChatClientProvider>();

        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        // Act
        var run = () => scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage(
            "No IConversationStore<Guid> is registered*The stores your app registered keep IAgentSettingsStore only, "
            + "and beside a registered store Typeshift keeps nothing in memory.");
    }

    [Fact]
    public async Task Given_A_Store_Of_Conversations_Alone_A_Run_That_Must_Ask_Fails_Naming_IDecisionStore()
    {
        // Arrange
        await using var provider = Provider(new StoredJsonStore(), new WarningLog());
        await using var scope = provider.CreateAsyncScope();
        ChatClient.Reply(Call(Data.Tool.CallId, "close_ticket", ("number", Data.Agent.Ticket.Default)));

        // Act
        var run = () => scope.ServiceProvider.GetRequiredService<AgentHost<GuardedAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*IDecisionStore*");
    }

    [Fact]
    public async Task Given_A_Store_Of_Conversations_Alone_A_Run_Naming_A_Mode_Fails_Naming_IDecisionStore()
    {
        // Arrange
        await using var provider = Provider(new StoredJsonStore(), new WarningLog());
        await using var scope = provider.CreateAsyncScope();

        // Act
        var run = () => scope.ServiceProvider.GetRequiredService<AgentHost<GuardedAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, settings: new() { Mode = AgentMode.Plan }, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*IDecisionStore*");
    }

    [Fact]
    public async Task Given_A_Run_Whose_Mode_No_Store_Keeps_The_Conversation_Is_Free_For_The_Next_Run()
    {
        // Arrange
        await using var provider = Provider(new StoredJsonStore(), new WarningLog());
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<GuardedAgent>>()
            .WithConversationId(Data.Agent.ConversationId);

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var refused = await Record.ExceptionAsync(() =>
            host.RunAsync(Data.Tool.Question, settings: new() { Mode = AgentMode.Plan }, ct: Ct));

        var response = await host.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        refused.Should().BeOfType<InvalidOperationException>();
        response.Text.Should().Be(Data.Tool.Answer);
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

    [Fact]
    public async Task Given_A_Watcher_Behind_By_More_Than_The_Limit_It_Receives_The_Newest_Updates()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        var realtime = new RealtimeTrackingOptions { PublishInterval = TimeSpan.Zero, MaxPendingUpdates = 2 };

        var services = new ServiceCollection();
        services.AddTypeshift().WithRealtimeTracking(realtime).WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var watch = provider.GetRequiredService<ConversationTracker<Guid>>()
            .WatchAsync(Data.Agent.ConversationId, watching.Token)
            .GetAsyncEnumerator(Ct);

        ChatClient.Reply(Text("a"), gate, Text("b"), Text("c"), Text("d"));

        var first = watch.MoveNextAsync();

        var run = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(Data.Tool.Question, ct: Ct)
            .ToListAsync(Ct);

        await first;

        // Act
        gate.SetResult();
        await run;

        // Assert
        (await watch.MoveNextAsync()).Should().BeTrue();
        watch.Current.Text.Should().Be("c");
        (await watch.MoveNextAsync()).Should().BeTrue();
        watch.Current.Text.Should().Be("d");
    }

    private static ChatResponseUpdate Answer(params AIContent[] contents) => new(ChatRole.Assistant, contents);

    // A container whose store fails every append, and runs that do what the option says then
    private ServiceProvider FailingAppends(StoreFailure failure)
    {
        var services = new ServiceCollection();

        services.AddTypeshift()
            .WithStore<FailingAppendStore>()
            .WithChatClientProvider<ScriptedChatClientProvider>()
            .WithOptions(options => options.StoreFailure = failure);

        services.AddSingleton(ChatClient).AddSingleton(Models);
        return services.BuildServiceProvider();
    }

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

    // Another instance of the app over the shared store: a container of its own, whose claims on a conversation the
    // others never see, on the test's conversation. With a hold, its tool calls wait until the hold is released
    private AgentHost<ProbeAgent> Instance(
        ITypeshiftStore<Guid> store,
        ScriptedChatClient client,
        ToolHold? hold = null)
    {
        var services = new ServiceCollection();
        var typeshift = services.AddTypeshift().WithStore(store).WithChatClientProvider<ScriptedChatClientProvider>();

        if (hold is not null) typeshift.WithToolMiddleware<HeldToolMiddleware>();

        services.AddSingleton<TimeProvider>(Time).AddSingleton(client).AddSingleton(Models);
        services.AddSingleton(hold ?? new ToolHold());

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();

        _instances.Add(scope);
        _instances.Add(provider);

        return scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId);
    }

    protected override async ValueTask DisposeCoreAsync()
    {
        foreach (var instance in _instances)
        {
            await instance.DisposeAsync();
        }
    }
}
