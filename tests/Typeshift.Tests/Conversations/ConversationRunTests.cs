using System.Text.Json;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Conversations.Support;
using Typeshift.Tests.Tools.Support;
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
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage(Data.Tool.Answer);
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
        await failed.Should().ThrowAsync<InvalidOperationException>().WithMessage(Data.Tool.Answer);

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_An_Interrupted_Typed_Run_Resuming_It_Typed_Returns_The_Result(bool streaming)
    {
        // Arrange
        var expected = new ProbeSettings { Limit = Data.Tool.Limit, Verbose = true };
        await StoreAsync(new ChatMessage(ChatRole.User, Data.Tool.Question));

        ChatClient.Reply(Text(JsonSerializer.Serialize(expected, Json.GetTypeInfo<ProbeSettings>())));
        var chat = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);

        // Act
        var response = streaming
            ? await ReadAsync(chat.ResumeStreamingAsync<ProbeSettings>(Ct))
            : await chat.ResumeAsync<ProbeSettings>(Ct);

        // Assert
        response.Result.Should().Be(expected);
        ChatClient.Options[0]!.ResponseFormat.Should().BeOfType<ChatResponseFormatJson>();
    }

    [Fact]
    public async Task Given_Steering_Input_With_An_Image_The_Model_Receives_It()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        var image = new DataContent(new byte[] { 1, 2, 3 }, "image/png");

        ChatClient
            .Reply(gate, Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.FollowUp));

        var run = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, Ct);
        await ChatClient.Requested;

        // Act
        var steered = Tracker.TrySteer(
            Data.Agent.ConversationId,
            new ChatMessage(ChatRole.User, [new TextContent(Data.Agent.Steering), image]));
        gate.SetResult();
        await run;

        // Assert
        steered.Should().BeTrue();
        ChatClient.Requests[1][^1].Contents.OfType<DataContent>().Should().Equal(image);
    }

    [Fact]
    public void Given_Steering_Input_Of_The_Assistant_Steering_Throws()
    {
        // Act
        var steer = () => Tracker.TrySteer(
            Data.Agent.ConversationId,
            new ChatMessage(ChatRole.Assistant, Data.Agent.Steering));

        // Assert
        steer.Should().Throw<ArgumentException>().WithParameterName("input");
    }

    [Fact]
    public void Given_Steering_Input_With_The_Mark_Of_A_Background_Answer_Steering_Throws()
    {
        // Arrange
        var forged = new ChatMessage(ChatRole.User, Data.Agent.Steering)
        {
            AdditionalProperties = new() { [ConversationRun.BackgroundCall] = Data.Tool.CallId }
        };

        // Act
        var steer = () => Tracker.TrySteer(Data.Agent.ConversationId, forged);

        // Assert
        steer.Should().Throw<ArgumentException>().WithParameterName("input");
    }

    [Fact]
    public async Task Given_Many_Steering_Inputs_A_Later_Call_Puts_Them_Back_In_Their_Order()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, Text(Data.Tool.Answer))
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Agent.FollowUp));

        var run = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, Ct);
        await ChatClient.Requested;

        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.CompanyName);
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Reason);

        // Act
        gate.SetResult();
        await run;

        // Assert
        ChatClient.Requests[2].Select(message => message.Role == ChatRole.User ? message.Text : message.Role.Value)
            .Should()
            .Equal(
                Data.Tool.Question,
                "assistant",
                Data.Agent.Steering,
                Data.Agent.CompanyName,
                Data.Agent.Reason,
                "assistant",
                "tool");
    }

    [Fact]
    public async Task Given_A_Full_Steering_Queue_Steering_Throws_A_ConversationBusyException()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.FollowUp));

        var run = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, Ct);
        await ChatClient.Requested;

        for (var input = 0; input < ConversationRun.MaxSteeringInputs; input++)
        {
            Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        }

        // Act
        var steer = () => Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);

        // Assert
        steer.Should()
            .Throw<ConversationBusyException>()
            .Where(busy => Equals(busy.Conversation, Data.Agent.ConversationId));
        gate.SetResult();
        await run;
    }

    [Fact]
    public async Task Given_Steering_Input_Longer_Than_A_Run_Holds_Steering_Throws_A_ConversationBusyException()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        ChatClient.Reply(gate, Text(Data.Tool.Answer));

        var run = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, Ct);
        await ChatClient.Requested;

        // Act
        var steer = () => Tracker.TrySteer(
            Data.Agent.ConversationId,
            new string('x', ConversationRun.MaxSteeringCharacters + 1));

        // Assert
        steer.Should().Throw<ConversationBusyException>();
        gate.SetResult();
        await run;
    }

    [Fact]
    public async Task Given_A_Full_Steering_Queue_Once_The_Model_Took_It_Steering_Is_Taken_Again()
    {
        // Arrange
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();

        ChatClient
            .Reply(first, Text(Data.Tool.Answer))
            .Reply(second, Text(Data.Agent.FollowUp))
            .Reply(Text(Data.Agent.Reason));

        var run = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, Ct);
        await ChatClient.Requested;

        for (var input = 0; input < ConversationRun.MaxSteeringInputs; input++)
        {
            Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        }

        first.SetResult();
        await ChatClient.RequestedAsync(2);

        // Act
        var steered = Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.CompanyName);
        second.SetResult();
        await run;

        // Assert
        steered.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_An_Answer_Stored_Without_Its_End_Resuming_Returns_It_Without_A_Model_Call(bool streaming)
    {
        // Arrange
        await StoreAsync(new ChatMessage(ChatRole.User, Data.Tool.Question));
        await StoreAsync(new ChatMessage(ChatRole.Assistant, Data.Tool.Answer));

        var chat = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);

        // Act
        var response = streaming
            ? (await chat.ResumeStreamingAsync(ct: Ct).ToListAsync(Ct)).ToChatResponse()
            : await chat.ResumeAsync(ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
        ChatClient.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_An_Answer_Stored_Without_Its_End_Resuming_Stores_The_End()
    {
        // Arrange
        await StoreAsync(new ChatMessage(ChatRole.User, Data.Tool.Question));
        await StoreAsync(new ChatMessage(ChatRole.Assistant, Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).ResumeAsync(ct: Ct);
        var entries = await EntriesAsync(Data.Agent.ConversationId);

        // Assert
        entries.Select(entry => entry.EndsRun).Should().Equal(false, false, true);
    }

    [Fact]
    public async Task Given_An_Answer_Stored_With_A_Call_In_Flight_Resuming_Calls_The_Model()
    {
        // Arrange
        await StoreAsync(new ChatMessage(ChatRole.User, Data.Tool.Question));

        await StoreAsync(new ChatMessage(
            ChatRole.Assistant,
            [new TextContent(Data.Agent.Reason), new FunctionCallContent(Data.Tool.CallId, "probe")]));

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).ResumeAsync(ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Run_That_Failed_At_The_Provider_Retrying_Sends_The_Input_Once()
    {
        // Arrange
        ChatClient
            .Reply(new HttpRequestException(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);
        await Record.ExceptionAsync(() => host.RunAsync(Data.Tool.Question, ct: Ct));

        // Act
        await host.RetryAsync(ct: Ct);

        // Assert
        ChatClient.Requests[1].Where(message => message.Text == Data.Tool.Question).Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Run_That_Failed_At_The_Provider_Retrying_Answers()
    {
        // Arrange
        ChatClient
            .Reply(new HttpRequestException(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);
        await Record.ExceptionAsync(() => host.RunAsync(Data.Tool.Question, ct: Ct));

        // Act
        var response = await host.RetryAsync(ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Run_That_Failed_At_The_Provider_Retrying_Stores_No_Input_Again()
    {
        // Arrange
        ChatClient
            .Reply(new HttpRequestException(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);
        await Record.ExceptionAsync(() => host.RunAsync(Data.Tool.Question, ct: Ct));

        // Act
        await host.RetryAsync(ct: Ct);
        var entries = await host.LoadConversationAsync(Ct);

        // Assert
        entries.SelectMany(entry => entry.Messages).Where(message => message.Role == ChatRole.User)
            .Should()
            .ContainSingle();
    }

    [Fact]
    public async Task Given_A_Run_That_Failed_At_The_Provider_A_Streaming_Retry_Answers()
    {
        // Arrange
        ChatClient
            .Reply(new HttpRequestException(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);
        await Record.ExceptionAsync(() => host.RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct).AsTask());

        // Act
        var updates = await host.RetryStreamingAsync(ct: Ct).ToListAsync(Ct);

        // Assert
        updates.ToChatResponse().Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Cancelled_Run_Retrying_Continues_It()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);
        using var cancelling = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var run = host.RunAsync(Data.Tool.Question, ct: cancelling.Token);
        await ChatClient.Requested.WaitAsync(Ct);
        await cancelling.CancelAsync();
        await Record.ExceptionAsync(() => run);

        // Act
        var response = await host.RetryAsync(ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Conversation_Whose_Last_Run_Answered_Retrying_Throws()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);
        await host.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        var retry = () => host.RetryAsync(ct: Ct);

        // Assert
        await retry.Should().ThrowAsync<NothingToResumeException>().WithMessage("*has no failed run to retry*");
    }

    [Fact]
    public async Task Given_An_Answer_Stored_With_An_Error_Resuming_Calls_The_Model()
    {
        // Arrange
        await StoreAsync(new ChatMessage(ChatRole.User, Data.Tool.Question));

        await StoreAsync(new ChatMessage(
            ChatRole.Assistant,
            [new TextContent(Data.Agent.Reason), new ErrorContent(Data.Agent.FollowUp)]));

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).ResumeAsync(ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Cancelled_Token_A_Run_Throws_Before_It_Calls_The_Model(bool streaming)
    {
        // Arrange
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        var chat = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);

        // Act
        var run = async () =>
        {
            if (streaming) await chat.RunStreamingAsync(Data.Tool.Question, ct: cancel.Token).ToListAsync(Ct);
            else await chat.RunAsync(Data.Tool.Question, ct: cancel.Token);
        };

        // Assert
        await run.Should().ThrowAsync<OperationCanceledException>();
        ChatClient.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Cancelled_Token_A_Run_Stores_Nothing_And_Leaves_The_Conversation_Free()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        var chat = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);
        await Record.ExceptionAsync(() => chat.RunAsync(Data.Tool.Question, ct: cancel.Token));

        // Act
        var response = await chat.RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
        (await EntriesAsync(Data.Agent.ConversationId)).SelectMany(entry => entry.Messages)
            .Select(message => message.Text)
            .Should()
            .NotContain(Data.Tool.Question);
    }

    [Fact]
    public async Task Given_A_Raw_Call_That_Failed_To_Begin_The_Next_Call_Of_The_Conversation_Runs()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);
        var options = await host.ResolveOptionsAsync(ct: Ct);
        using var client = await host.CreateChatClientAsync(options, Ct);
        await Record.ExceptionAsync(() => client.GetResponseAsync([], options, Ct));

        // Act
        var response = await client.GetResponseAsync(Data.Tool.Question, options, Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Provider_That_Keeps_The_Conversation_A_Call_Id_It_Uses_Again_Stays_Its_Own(bool streaming)
    {
        // Arrange
        var again = Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query));
        again.ConversationId = Data.Persistence.ChatName;

        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer))
            .Reply(again)
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(Data.Tool.Question, streaming, Ct);

        // Act
        await RunAsync(Data.Agent.FollowUp, streaming, Ct);

        // Assert
        (await EntriesAsync(Data.Agent.ConversationId))
            .SelectMany(entry => entry.Messages)
            .SelectMany(message => message.Contents)
            .OfType<FunctionCallContent>()
            .Select(call => call.CallId)
            .Should()
            .Equal(Data.Tool.CallId, Data.Tool.CallId);
    }

    [Fact]
    public async Task Given_An_Interrupted_Response_Of_Text_And_A_Call_Resuming_Keeps_The_Text_And_Drops_The_Call()
    {
        // Arrange
        var answer = new ChatMessage(
            ChatRole.Assistant,
            [new TextContent(Data.Agent.Reason), new FunctionCallContent(Data.Tool.CallId, "probe")]);

        await StoreAsync(new ChatMessage(ChatRole.User, Data.Tool.Question));
        await StoreAsync(answer);
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).ResumeAsync(ct: Ct);

        // Assert
        ChatClient.Assert.Request(0).Texts.Should().Equal(Data.Tool.Question, Data.Agent.Reason);
        ChatClient.Assert.Request(0).Calls.Should().BeEmpty();
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
        await Record.ExceptionAsync(() => second.WaitAsync(Ct));
        var ending = !first.IsCompleted;

        // Assert
        second.IsCanceled.Should().BeTrue();
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
    public async Task Given_A_Watcher_That_Joins_Late_It_Receives_The_Text_So_Far_First()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        ChatClient.Reply(Text("a"), Text("b"), gate, Text("c"));

        await using var run = Host<ProbeAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(Data.Tool.Question, ct: Ct)
            .GetAsyncEnumerator(Ct);

        await run.MoveNextAsync();
        await run.MoveNextAsync();

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var watch = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).GetAsyncEnumerator(Ct);

        // Act
        await watch.MoveNextAsync();

        // Assert
        watch.Current.Text.Should().Be("ab");
    }

    [Fact]
    public async Task Given_A_Watcher_That_Joins_Late_It_Receives_Nothing_The_Store_Holds()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text("a"), gate, Text("b"));

        await using var run = Host<ProbeAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(Data.Tool.Question, ct: Ct)
            .GetAsyncEnumerator(Ct);

        while (await run.MoveNextAsync() && run.Current.Text != "a")
        {
        }

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var watch = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).GetAsyncEnumerator(Ct);

        // Act
        await watch.MoveNextAsync();

        // Assert
        watch.Current.Contents.Should().ContainSingle(content => content is TextContent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Run_That_Answers_Its_Watchers_Receive_Its_End_With_The_Finish_Reason(bool streaming)
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer), new ChatResponseUpdate { FinishReason = ChatFinishReason.Stop });

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var watched = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token)
            .Where(update => update.EndsRun)
            .Take(1)
            .ToListAsync(Ct);

        var host = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);

        // Act
        if (streaming) await host.RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct);
        else await host.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        (await watched).Single().FinishReason.Should().Be(ChatFinishReason.Stop);
    }

    [Fact]
    public async Task Given_A_Run_That_Fails_Its_Watchers_Last_Update_Holds_The_Error_Its_End_Stores()
    {
        // Arrange
        ChatClient.Reply(new InvalidOperationException(Data.Agent.Reason));

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var watched = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).Take(1).ToListAsync(Ct);
        var host = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);

        // Act
        await Record.ExceptionAsync(() => host.RunAsync(Data.Tool.Question, ct: Ct));

        // Assert
        (await watched).Single().Contents.OfType<ErrorContent>().Select(error => error.Message)
            .Should()
            .Equal(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Stream_That_Fails_After_Text_A_Watcher_Still_Receives_The_Text_That_Waited()
    {
        // Arrange
        ChatClient.Reply(Text("a"), Text("b"), new InvalidOperationException(Data.Agent.Reason));

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var watch = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).GetAsyncEnumerator(Ct);
        var first = watch.MoveNextAsync();

        // Act
        await Record.ExceptionAsync(() => RunAsync(Data.Tool.Question, streaming: true, Ct));

        // Assert
        (await first).Should().BeTrue();
        (await watch.MoveNextAsync()).Should().BeTrue();
        watch.Current.Text.Should().Be("b");
    }

    [Fact]
    public async Task Given_Text_A_Whole_Publish_Interval_After_The_Last_A_Watcher_Receives_It_At_Once()
    {
        // Arrange
        var later = new TaskCompletionSource();
        ChatClient.Reply(Text("a"), later, Text("b"), Text("c"));

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var watch = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).GetAsyncEnumerator(Ct);
        var first = watch.MoveNextAsync();

        var run = RunAsync(Data.Tool.Question, streaming: true, Ct);
        await first;
        Time.Advance(Services.GetRequiredService<LiveUpdateOptions>().PublishInterval);

        // Act
        later.SetResult();
        await run;

        // Assert
        (await watch.MoveNextAsync()).Should().BeTrue();
        watch.Current.Text.Should().Be("b");
    }

    [Fact]
    public async Task Given_Text_Of_Another_Role_Within_The_Interval_A_Watcher_Receives_It_Apart()
    {
        // Arrange
        ChatClient.Reply(Text("a"), Text("b"), new ChatResponseUpdate(ChatRole.System, "c"));

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var watch = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).GetAsyncEnumerator(Ct);
        var first = watch.MoveNextAsync();

        // Act
        await RunAsync(Data.Tool.Question, streaming: true, Ct);

        // Assert
        (await first).Should().BeTrue();
        (await watch.MoveNextAsync()).Should().BeTrue();
        watch.Current.Text.Should().Be("b");
    }

    [Fact]
    public async Task Given_The_Last_Watcher_Left_The_Conversations_Live_State_Goes_With_It()
    {
        // Arrange
        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var watch = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).GetAsyncEnumerator(Ct);
        var next = watch.MoveNextAsync();
        var conversation = new StoreId<Guid>(Data.Agent.ConversationId);
        var live = Services.GetRequiredService<LiveRuns>().Enter(conversation, _ => { });

        // Act
        await watching.CancelAsync();
        await Record.ExceptionAsync(async () => await next);
        await watch.DisposeAsync();

        // Assert
        live.Removed.Should().BeTrue();
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Steered_Run_A_Watcher_Receives_The_First_Answer_Before_The_Next_Call_Answers(
        bool streaming)
    {
        // Arrange
        var answering = new TaskCompletionSource();
        var next = new TaskCompletionSource();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(answering, Text(Data.Tool.Answer))
            .Reply(next, Text(Data.Agent.FollowUp));

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var watch = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).GetAsyncEnumerator(Ct);
        var call = watch.MoveNextAsync();

        var run = RunAsync(Data.Tool.Question, streaming, Ct);
        await ChatClient.RequestedAsync(2);
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        answering.SetResult();
        await ChatClient.RequestedAsync(3);

        // Act
        await call;
        await watch.MoveNextAsync();
        var answered = await watch.MoveNextAsync().AsTask().WaitAsync(Ct);
        var answer = watch.Current.Text;

        // Assert
        answered.Should().BeTrue();
        answer.Should().Be(Data.Tool.Answer);
        next.SetResult();
        await run;
    }

    [Fact]
    public async Task Given_Text_Of_Two_Messages_Within_The_Interval_A_Watcher_Receives_Each_Apart()
    {
        // Arrange
        var second = new ChatResponseUpdate(ChatRole.Assistant, "b") { MessageId = Data.Tool.CallId };
        var third = new ChatResponseUpdate(ChatRole.Assistant, "c") { MessageId = Data.Tool.SecondCallId };
        ChatClient.Reply(Text("a"), second, third);

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var watch = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).GetAsyncEnumerator(Ct);
        var first = watch.MoveNextAsync();

        // Act
        await RunAsync(Data.Tool.Question, streaming: true, Ct);

        // Assert
        (await first).Should().BeTrue();
        (await watch.MoveNextAsync()).Should().BeTrue();
        watch.Current.Text.Should().Be("b");
        (await watch.MoveNextAsync()).Should().BeTrue();
        watch.Current.Text.Should().Be("c");
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

    // A step of an earlier run, stored as its run would have
    private Task StoreAsync(ChatMessage message) =>
        Services.GetRequiredService<ITypeshiftStore<Guid>>().AppendConversationAsync(
            Data.Agent.ConversationId,
            new ConversationEntry("probe_agent", [message], Time.GetUtcNow()),
            Ct);

    // The same run on either path, a streamed one as the response its updates make
    private async Task<ChatResponse> RunAsync(string input, bool streaming, CancellationToken ct)
    {
        var chat = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);

        return streaming
            ? (await chat.RunStreamingAsync(input, ct: ct).ToListAsync(ct)).ToChatResponse()
            : await chat.RunAsync(input, ct: ct);
    }

    // A typed stream read to its end, as its response
    private async Task<ChatResponse<TResult>> ReadAsync<TResult>(StreamingRun<TResult> run)
    {
        await run.ToListAsync(Ct);
        return run.Response;
    }
}
