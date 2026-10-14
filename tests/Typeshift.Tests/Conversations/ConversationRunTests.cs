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

        var host = Host<ProbeAgent>();
        await host.RunAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct);

        // Act
        await host.RunAsync(Data.Agent.FollowUp, conversationId: Data.Agent.ConversationId, ct: Ct);

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
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct);
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
            .RunStreamingAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct)
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

    [Fact]
    public async Task Given_A_Run_Only_Its_Last_Entry_Ends_It()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct);
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
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct);
        var response = (await EntriesAsync(Data.Agent.ConversationId))[1];

        // Assert
        (response.Model, response.Agent, response.Messages[0].AuthorName)
            .Should()
            .Be((ProbeAgent.Model, "probe_agent", "probe_agent"));
    }

    [Fact]
    public async Task Given_A_Conversation_The_Provider_Never_Receives_Its_Id()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct);

        // Assert
        ChatClient.Options[0]!.ConversationId.Should().BeNull();
    }

    [Fact]
    public async Task Given_A_Failing_Run_It_Ends_With_Its_Error_And_The_Exception_Surfaces()
    {
        // Arrange
        ChatClient.Reply(new InvalidOperationException(Data.Tool.Answer));

        var host = Host<ProbeAgent>();

        // Act
        var run = () => host.RunAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>();
        (await EntriesAsync(Data.Agent.ConversationId))[^1].Messages[0].Contents
            .Should()
            .ContainSingle(content => content is ErrorContent);
    }

    [Fact]
    public async Task Given_A_Failed_Run_The_Next_Run_Leaves_Out_Its_Error()
    {
        // Arrange
        ChatClient
            .Reply(new InvalidOperationException(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>();
        var failed = () => host.RunAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct);
        await failed.Should().ThrowAsync<InvalidOperationException>();

        // Act
        await host.RunAsync(Data.Agent.FollowUp, conversationId: Data.Agent.ConversationId, ct: Ct);

        // Assert
        ChatClient.Requests[1].Select(message => message.Text).Should().Equal(Data.Tool.Question, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_A_Stream_Left_Early_The_Run_Ends_With_The_Updates_That_Arrived()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer), Text(Data.Agent.FollowUp));

        var stream = Host<ProbeAgent>().RunStreamingAsync(
            Data.Tool.Question,
            conversationId: Data.Agent.ConversationId,
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
        var store = Services.GetRequiredService<ITypeshiftStore>();
        var input = new ChatMessage(ChatRole.User, Data.Tool.Question);
        var call = new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(Data.Tool.CallId, "probe")]);

        var conversation = Data.Agent.ConversationId;

        await store.AppendConversationAsync(conversation, new("probe_agent", [input], Time.GetUtcNow()), Ct);
        await store.AppendConversationAsync(conversation, new("probe_agent", [call], Time.GetUtcNow()), Ct);

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ProbeAgent>().ResumeAsync(Data.Agent.ConversationId, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
        ChatClient.Requests[0].Select(message => message.Text).Should().Equal(Data.Tool.Question);
    }

    [Fact]
    public async Task Given_A_Conversation_Whose_Last_Run_Ended_Resuming_Fails()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct);

        // Act
        var resume = () => Host<ProbeAgent>().ResumeAsync(Data.Agent.ConversationId, ct: Ct);

        // Assert
        await resume.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no interrupted run*");
    }

    [Fact]
    public async Task Given_A_Run_Going_On_A_Second_Run_Of_The_Conversation_Fails()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        ChatClient.Reply(gate, Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>();
        var first = host.RunAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct);
        await ChatClient.Requested;

        // Act
        var second = () => host.RunAsync(Data.Agent.FollowUp, conversationId: Data.Agent.ConversationId, ct: Ct);

        // Assert
        await second.Should().ThrowAsync<InvalidOperationException>().WithMessage("*TrySteer*");
        gate.SetResult();
        await first;
    }

    [Fact]
    public async Task Given_A_Run_Going_On_The_Tracker_Lists_It()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        ChatClient.Reply(gate, Text(Data.Tool.Answer));

        var run = Host<ProbeAgent>().RunAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct);
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

    [Fact]
    public async Task Given_Steering_Input_The_Run_Answers_It_Before_It_Ends()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.FollowUp));

        var run = Host<ProbeAgent>().RunAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct);
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

        var run = Host<ProbeAgent>().RunAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct);
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

        var run = Host<ProbeAgent>().RunAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct);
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
            .RunStreamingAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct)
            .ToListAsync(Ct);

        // Assert
        (await next).Should().BeTrue();
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
            .RunStreamingAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct)
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
    public async Task Given_A_Conversation_Id_In_The_Options_The_Raw_Path_Stores_The_Run()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>();
        var options = await host.ResolveOptionsAsync(ct: Ct);
        options.ConversationId = Data.Agent.ConversationId.ToString();

        // Act
        using var client = await host.CreateChatClientAsync(options, Ct);
        await client.GetResponseAsync([new(ChatRole.User, Data.Tool.Question)], options, Ct);

        // Assert
        (await EntriesAsync(Data.Agent.ConversationId)).Should().HaveCount(3);
    }
}
