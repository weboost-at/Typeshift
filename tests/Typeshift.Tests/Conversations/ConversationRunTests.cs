using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Conversations.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Conversations;

public class ConversationRunTests : AgentTestBase
{
    [Fact]
    public async Task Given_A_Conversation_A_Second_Run_Starts_From_The_First()
    {
        // Arrange
        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);
        await host.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await host.RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        ChatClient.Requests[1].Select(message => message.Text)
            .Should()
            .Equal(Data.Tool.Question, Data.Tool.Answer, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_A_Run_Its_Input_Responses_And_Tool_Results_Are_Stored_Step_By_Step()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);
        var entries = await EntriesAsync(Data.Agent.ConversationId);
        var contents = entries.Select(entry => entry.Messages.SelectMany(message => message.Contents));

        // Assert
        contents.Select(entry => entry.Select(content => content.GetType()))
            .Should()
            .BeEquivalentTo(
                [
                    [typeof(TextContent)],
                    [typeof(FunctionCallContent)],
                    [typeof(FunctionResultContent)],
                    [typeof(TextContent)],
                    Array.Empty<Type>()
                ],
                options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Its_Responses_And_Tool_Results_Are_Stored_Step_By_Step()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(Data.Tool.Question, ct: Ct)
            .ToListAsync(Ct);

        var entries = await EntriesAsync(Data.Agent.ConversationId);
        var contents = entries.Select(entry => entry.Messages.SelectMany(message => message.Contents));

        // Assert
        contents.Select(entry => entry.Select(content => content.GetType()))
            .Should()
            .BeEquivalentTo(
                [
                    [typeof(TextContent)],
                    [typeof(FunctionCallContent)],
                    [typeof(FunctionResultContent)],
                    [typeof(TextContent)],
                    Array.Empty<Type>()
                ],
                options => options.WithStrictOrdering());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Run_Only_Its_Last_Entry_Ends_It(bool streaming)
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question, streaming, Ct);
        var entries = await EntriesAsync(Data.Agent.ConversationId);

        // Assert
        entries.Select(entry => entry.EndsRun).Should().Equal(false, false, true);
    }

    [Fact]
    public async Task Given_A_Model_Response_Its_Entry_Carries_The_Model_And_The_Agent()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);
        var response = (await EntriesAsync(Data.Agent.ConversationId))[1];

        // Assert
        (response.Model, response.Agent, response.Messages[0].AuthorName)
            .Should()
            .Be((ProbeAgent.Model, "probe_agent", "probe_agent"));
    }

    [Fact]
    public async Task Given_A_Run_That_Names_A_Provider_The_Entries_Of_Its_Model_Responses_Carry_It()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);
        var entries = await EntriesAsync(Data.Agent.ConversationId);

        // Assert
        entries.Select(entry => entry.Provider)
            .Should()
            .Equal(null, ProbeAgent.ModelProvider, null, ProbeAgent.ModelProvider, null);
    }

    [Fact]
    public async Task Given_Settings_That_Name_Another_Provider_The_Response_Entry_Carries_That_Provider()
    {
        // Arrange
        var settings = new ProbeAgent.Settings { Provider = Data.Agent.Provider };
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, settings, Ct);

        var response = (await EntriesAsync(Data.Agent.ConversationId))[1];

        // Assert
        response.Provider.Should().Be(Data.Agent.Provider);
    }

    [Fact]
    public async Task Given_A_Conversation_The_Provider_Never_Receives_Its_Id()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ChatClient.Options[0]!.ConversationId.Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Failing_Run_It_Ends_With_Its_Error_And_The_Exception_Surfaces(bool streaming)
    {
        // Arrange
        ChatClient.Reply(new InvalidOperationException(Data.Tool.Answer));

        // Act
        var run = () => RunAsync(Data.Tool.Question, streaming, Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>();
        (await EntriesAsync(Data.Agent.ConversationId))[^1].Messages[0].Contents
            .Should()
            .ContainSingle(content => content is ErrorContent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Failing_Run_Its_Ending_Entry_Holds_The_Exceptions_Message(bool streaming)
    {
        // Arrange
        ChatClient.Reply(new InvalidOperationException(Data.Agent.Reason));

        await Record.ExceptionAsync(() => RunAsync(Data.Tool.Question, streaming, Ct));

        // Act
        var entries = await EntriesAsync(Data.Agent.ConversationId);

        // Assert
        entries[^1].Messages[0].Contents.OfType<ErrorContent>()
            .Select(error => error.Message)
            .Should()
            .Equal(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Stream_Left_Early_Its_Ending_Entry_Says_The_Run_Stopped_Before_It_Answered()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer), new TaskCompletionSource(), Text(Data.Agent.FollowUp));

        var updates = Host<ProbeAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(Data.Tool.Question, ct: Ct)
            .GetAsyncEnumerator(Ct);

        await updates.MoveNextAsync();

        // Act
        await updates.DisposeAsync().AsTask().WaitAsync(Ct);
        var entries = await EntriesAsync(Data.Agent.ConversationId);

        // Assert
        entries[^1].Messages[0].Contents.OfType<ErrorContent>()
            .Select(error => error.Message)
            .Should()
            .Equal("The run stopped before it answered.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Cancelled_Run_It_Ends_With_An_Error_And_The_Cancellation_Surfaces(bool streaming)
    {
        // Arrange
        ChatClient.Reply(new TaskCompletionSource(), Text(Data.Tool.Answer));

        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var run = RunAsync(Data.Tool.Question, streaming, cancel.Token);
        await ChatClient.Requested;

        // Act
        await cancel.CancelAsync();
        var cancelled = await Record.ExceptionAsync(() => run);
        var entries = await EntriesAsync(Data.Agent.ConversationId);

        // Assert
        cancelled.Should().BeAssignableTo<OperationCanceledException>();
        entries[^1].EndsRun.Should().BeTrue();
        entries[^1].Messages[0].Contents.Should().ContainSingle(content => content is ErrorContent);
    }

    [Fact]
    public async Task Given_A_Failed_Run_The_Next_Run_Leaves_Out_Its_Error()
    {
        // Arrange
        ChatClient
            .Reply(new InvalidOperationException(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);
        var failed = () => host.RunAsync(Data.Tool.Question, ct: Ct);
        await failed.Should().ThrowAsync<InvalidOperationException>();

        // Act
        await host.RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        ChatClient.Requests[1].Select(message => message.Text).Should().Equal(Data.Tool.Question, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_A_Stream_Left_Early_The_Run_Ends_With_The_Updates_That_Arrived()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer), Text(Data.Agent.FollowUp));

        var stream = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunStreamingAsync(
            Data.Tool.Question,
            ct: Ct);

        // Act
        await foreach (var _ in stream)
        {
            break;
        }

        var entries = await EntriesAsync(Data.Agent.ConversationId);

        // Assert
        entries[1].Messages[0].Text.Should().Be(Data.Tool.Answer);
        entries[^1].EndsRun.Should().BeTrue();
    }

    [Fact]
    public async Task Given_An_Interrupted_Run_Resuming_Continues_From_Its_Last_Step_Without_The_Call_In_Flight()
    {
        // Arrange
        var store = Services.GetRequiredService<ITypeshiftStore<Guid>>();
        var input = new ChatMessage(ChatRole.User, Data.Tool.Question);
        var call = new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(Data.Tool.CallId, "probe")]);

        var conversation = Data.Agent.ConversationId;

        await store.AppendConversationAsync(conversation, new("probe_agent", [input], Time.GetUtcNow()), Ct);
        await store.AppendConversationAsync(conversation, new("probe_agent", [call], Time.GetUtcNow()), Ct);

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).ResumeAsync(ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
        ChatClient.Requests[0].Select(message => message.Text).Should().Equal(Data.Tool.Question);
    }

    [Fact]
    public async Task Given_An_Interrupted_Run_Resuming_It_Streaming_Continues_From_Its_Last_Step()
    {
        // Arrange
        var store = Services.GetRequiredService<ITypeshiftStore<Guid>>();
        var input = new ChatMessage(ChatRole.User, Data.Tool.Question);

        await store.AppendConversationAsync(
            Data.Agent.ConversationId,
            new("probe_agent", [input], Time.GetUtcNow()),
            Ct);

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var updates = await Host<ProbeAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .ResumeStreamingAsync(ct: Ct)
            .ToListAsync(Ct);

        // Assert
        updates.ToChatResponse().Text.Should().Be(Data.Tool.Answer);
        ChatClient.Requests[0].Select(message => message.Text).Should().Equal(Data.Tool.Question);
    }

    [Fact]
    public async Task Given_A_Conversation_Whose_Last_Run_Ended_Resuming_Fails()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        await Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        var resume = () => Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).ResumeAsync(ct: Ct);

        // Assert
        await resume.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no interrupted run*");
    }

    [Fact]
    public async Task Given_A_Conversation_Whose_Last_Run_Ended_Resuming_Throws_A_NothingToResumeException()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        await Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        var resume = () => Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).ResumeAsync(ct: Ct);

        // Assert
        await resume.Should()
            .ThrowAsync<NothingToResumeException>()
            .Where(nothing => Equals(nothing.Conversation, Data.Agent.ConversationId));
    }

    [Fact]
    public async Task Given_A_Run_That_Failed_To_Begin_The_Next_Run_Of_The_Conversation_Runs()
    {
        // Arrange
        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason));

        var host = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);
        await host.RunAsync(Data.Tool.Question, ct: Ct);
        await Record.ExceptionAsync(() => host.ResumeAsync(ct: Ct));

        // Act
        var response = await host.RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Run_Going_On_A_Second_Run_Of_The_Conversation_Fails()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        ChatClient.Reply(gate, Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);
        var first = host.RunAsync(Data.Tool.Question, ct: Ct);
        await ChatClient.Requested;

        // Act
        var second = () => host.RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        await second.Should().ThrowAsync<InvalidOperationException>().WithMessage("*TrySteer*");
        gate.SetResult();
        await first;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Run_Going_On_A_Second_Run_Throws_A_ConversationBusyException(bool streaming)
    {
        // Arrange
        var gate = new TaskCompletionSource();
        ChatClient.Reply(gate, Text(Data.Tool.Answer));

        var first = RunAsync(Data.Tool.Question, streaming: false, Ct);
        await ChatClient.Requested;

        // Act
        var second = () => RunAsync(Data.Agent.FollowUp, streaming, Ct);

        // Assert
        await second.Should()
            .ThrowAsync<ConversationBusyException>()
            .Where(busy => Equals(busy.Conversation, Data.Agent.ConversationId));
        gate.SetResult();
        await first;
    }

    [Fact]
    public async Task Given_A_Run_Refused_As_Busy_The_Conversation_Stores_Nothing_Of_It()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        ChatClient.Reply(gate, Text(Data.Tool.Answer));

        var first = RunAsync(Data.Tool.Question, streaming: false, Ct);
        await ChatClient.Requested;
        await Record.ExceptionAsync(() => RunAsync(Data.Agent.FollowUp, streaming: false, Ct));

        // Act
        gate.SetResult();
        await first;

        var entries = await EntriesAsync(Data.Agent.ConversationId);

        // Assert
        entries.SelectMany(entry => entry.Messages).Select(message => message.Text)
            .Should()
            .Equal(Data.Tool.Question, Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_Steering_Refused_While_The_Run_Stores_Its_End_A_New_Run_Waits_And_Answers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<GatedEndStore>().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId);
        var store = provider.GetRequiredService<GatedEndStore>();
        var tracker = provider.GetRequiredService<ConversationTracker<Guid>>();

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason));

        var first = host.RunAsync(Data.Tool.Question, ct: Ct);
        await store.Ending.Task.WaitAsync(Ct);
        var steered = tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.FollowUp);

        // Act
        var second = host.RunAsync(Data.Agent.FollowUp, ct: Ct);
        store.Gate.SetResult();
        await first;
        var response = await second;

        // Assert
        steered.Should().BeFalse();
        response.Text.Should().Be(Data.Agent.Reason);
        ChatClient.Requests[1].Select(message => message.Text)
            .Should()
            .Equal(Data.Tool.Question, Data.Tool.Answer, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_A_Run_Waiting_For_A_Run_That_Is_Ending_Cancelling_It_Ends_The_Wait()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<GatedEndStore>().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId);
        var store = provider.GetRequiredService<GatedEndStore>();

        ChatClient.Reply(Text(Data.Tool.Answer));

        var first = host.RunAsync(Data.Tool.Question, ct: Ct);
        await store.Ending.Task.WaitAsync(Ct);

        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var second = host.RunAsync(Data.Agent.FollowUp, ct: cancel.Token);

        // Act
        await cancel.CancelAsync();
        var waited = await Record.ExceptionAsync(() => second);
        var ending = !first.IsCompleted;

        // Assert
        waited.Should().BeAssignableTo<OperationCanceledException>();
        ending.Should().BeTrue();
        store.Gate.SetResult();
        await first;
    }

    [Fact]
    public async Task Given_A_Run_Going_On_The_Tracker_Lists_It()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        ChatClient.Reply(gate, Text(Data.Tool.Answer));

        var run = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);
        await ChatClient.Requested;

        // Act
        var runs = Tracker.Runs;

        // Assert
        runs.Should().ContainSingle();
        runs[0].Should().BeEquivalentTo(new
        {
            Conversation = Data.Agent.ConversationId,
            Agent = "probe_agent",
            ProbeAgent.Model
        });
        gate.SetResult();
        await run;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Run_Watcher_It_Hears_The_Run_Start_And_End(bool streaming)
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var changes = Tracker.WatchRunsAsync(Ct).Take(2).ToListAsync(Ct);

        // Act
        await RunAsync(Data.Tool.Question, streaming, Ct);

        // Assert
        (await changes).Select(run => (run.Conversation, run.Ended))
            .Should()
            .Equal((Data.Agent.ConversationId, false), (Data.Agent.ConversationId, true));
    }

    [Fact]
    public async Task Given_A_Run_Going_On_A_Run_Watcher_Hears_It_Once_Then_Its_End()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        ChatClient.Reply(gate, Text(Data.Tool.Answer));

        var run = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);
        await ChatClient.Requested;

        // Act
        var changes = Tracker.WatchRunsAsync(Ct).Take(2).ToListAsync(Ct);
        gate.SetResult();
        await run;

        // Assert
        (await changes).Select(change => change.Ended).Should().Equal(false, true);
    }

    [Fact]
    public async Task Given_Steering_Input_The_Run_Answers_It_Before_It_Ends()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.FollowUp));

        var run = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);
        await ChatClient.Requested;

        // Act
        var steered = Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        gate.SetResult();
        var response = await run;

        // Assert
        steered.Should().BeTrue();
        ChatClient.Requests[1].Select(message => message.Text)
            .Should()
            .Equal(Data.Tool.Question, Data.Tool.Answer, Data.Agent.Steering);
        response.Messages.Select(message => message.Text).Should().Equal(Data.Tool.Answer, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_Steering_Input_During_The_Answer_To_Steering_The_Run_Answers_Both_In_Order()
    {
        // Arrange
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();

        ChatClient
            .Reply(first, Text(Data.Tool.Answer))
            .Reply(second, Text(Data.Agent.FollowUp))
            .Reply(Text(Data.Tool.Question));

        var run = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);
        await ChatClient.Requested;

        // Act
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        first.SetResult();
        await ChatClient.RequestedAsync(2);
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.CompanyName);
        second.SetResult();
        await run;

        // Assert
        ChatClient.Requests[2].Select(message => message.Text)
            .Should()
            .Equal(
                Data.Tool.Question,
                Data.Tool.Answer,
                Data.Agent.Steering,
                Data.Agent.FollowUp,
                Data.Agent.CompanyName);
    }

    [Fact]
    public async Task Given_Steering_Input_It_Is_Stored_With_The_Conversation()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.FollowUp));

        var run = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);
        await ChatClient.Requested;

        // Act
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        gate.SetResult();
        await run;

        var entries = await EntriesAsync(Data.Agent.ConversationId);

        // Assert
        entries.SelectMany(entry => entry.Messages).Select(message => message.Text)
            .Should()
            .Equal(Data.Tool.Question, Data.Tool.Answer, Data.Agent.Steering, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_A_Steered_Streaming_Run_Of_Two_Answers_Each_Is_Stored_Once()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(Text("a"), Text("b"), Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(gate, Text("c"), Text("d"))
            .Reply(Text(Data.Agent.FollowUp));

        var run = Host<ProbeAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(Data.Tool.Question, ct: Ct)
            .ToListAsync(Ct);

        await ChatClient.RequestedAsync(2);

        // Act
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        gate.SetResult();
        await run;

        var entries = await EntriesAsync(Data.Agent.ConversationId);

        // Assert
        entries.SelectMany(entry => entry.Messages).Select(message => message.Text)
            .Should()
            .Equal(Data.Tool.Question, "ab", "", "cd", Data.Agent.Steering, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_A_Run_That_Is_Ending_Steering_Returns_False_And_The_Input_Is_Not_Stored()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<GatedEndStore>().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId);
        var store = provider.GetRequiredService<GatedEndStore>();
        var tracker = provider.GetRequiredService<ConversationTracker<Guid>>();

        ChatClient.Reply(Text(Data.Tool.Answer));

        var run = host.RunAsync(Data.Tool.Question, ct: Ct);
        await store.Ending.Task.WaitAsync(Ct);

        // Act
        var steered = tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        store.Gate.SetResult();
        await run;

        var entries = await store.LoadConversationAsync(Data.Agent.ConversationId, Ct);

        // Assert
        steered.Should().BeFalse();
        entries.SelectMany(entry => entry.Messages).Select(message => message.Text)
            .Should()
            .Equal(Data.Tool.Question, Data.Tool.Answer);
    }

    [Fact]
    public void Given_No_Run_Going_On_Steering_Returns_False()
    {
        // Act
        var steered = Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);

        // Assert
        steered.Should().BeFalse();
    }

    [Fact]
    public async Task Given_A_Watcher_It_Receives_The_Updates_Of_A_Streaming_Run()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var watch = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).GetAsyncEnumerator(Ct);
        var next = watch.MoveNextAsync();

        // Act
        await Host<ProbeAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(Data.Tool.Question, ct: Ct)
            .ToListAsync(Ct);

        // Assert
        (await next).Should().BeTrue();
        watch.Current.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Watcher_It_Receives_Each_Step_Of_A_Run_That_Does_Not_Stream()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var watch = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).GetAsyncEnumerator(Ct);
        var first = watch.MoveNextAsync();

        // Act
        await Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        (await first).Should().BeTrue();
        watch.Current.Contents.Should().ContainSingle(content => content is FunctionCallContent);
        (await watch.MoveNextAsync()).Should().BeTrue();
        watch.Current.Contents.Should().ContainSingle(content => content is FunctionResultContent);
        (await watch.MoveNextAsync()).Should().BeTrue();
        watch.Current.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_Text_Within_The_Interval_A_Watcher_Receives_It_Merged_Ahead_Of_The_Next_Call()
    {
        // Arrange
        ChatClient
            .Reply(Text("a"), Text("b"), Text("c"), Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var watch = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).GetAsyncEnumerator(Ct);
        var first = watch.MoveNextAsync();

        // Act
        await Host<ProbeAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(Data.Tool.Question, ct: Ct)
            .ToListAsync(Ct);

        // Assert
        (await first).Should().BeTrue();
        watch.Current.Text.Should().Be("a");
        (await watch.MoveNextAsync()).Should().BeTrue();
        watch.Current.Text.Should().Be("bc");
        (await watch.MoveNextAsync()).Should().BeTrue();
        watch.Current.Contents.Should().ContainSingle(content => content is FunctionCallContent);
    }

    [Fact]
    public async Task Given_Text_Within_The_Interval_The_Merged_Update_Keeps_The_First_Updates_Ids()
    {
        // Arrange
        var second = new ChatResponseUpdate(ChatRole.Assistant, "b") { MessageId = Data.Tool.CallId };
        ChatClient.Reply(Text("a"), second, Text("c"));

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var watch = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).GetAsyncEnumerator(Ct);
        var first = watch.MoveNextAsync();

        // Act
        await Host<ProbeAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(Data.Tool.Question, ct: Ct)
            .ToListAsync(Ct);

        // Assert
        (await first).Should().BeTrue();
        (await watch.MoveNextAsync()).Should().BeTrue();
        watch.Current.Text.Should().Be("bc");
        watch.Current.MessageId.Should().Be(Data.Tool.CallId);
    }

    // The same run on either path, a streamed one as the response its updates make
    private async Task<ChatResponse> RunAsync(string input, bool streaming, CancellationToken ct)
    {
        var chat = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);

        return streaming
            ? (await chat.RunStreamingAsync(input, ct: ct).ToListAsync(ct)).ToChatResponse()
            : await chat.RunAsync(input, ct: ct);
    }
}
