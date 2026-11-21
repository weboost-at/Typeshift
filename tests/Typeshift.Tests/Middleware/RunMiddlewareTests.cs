using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Middleware.Support;
using Typeshift.Tests.MultiAgent.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Middleware;

public class RunMiddlewareTests : MiddlewareTestBase
{
    [Fact]
    public async Task Given_Run_Middleware_It_Wraps_The_Run_The_First_Registered_Outermost()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        Script.Log.Should().Equal(
            "run 0 before",
            "run 1 before",
            $"run 1 after: {Data.Tool.Answer}",
            $"run 0 after: {Data.Tool.Answer}");
    }

    [Fact]
    public async Task Given_Run_Middleware_That_Changes_The_Messages_The_Model_Receives_Them()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        Script.Runs[0] = (run, next) =>
        {
            run.Messages.Add(new ChatMessage(ChatRole.User, Data.Agent.FollowUp));
            return next();
        };

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        ChatClient.Requests[0].Select(message => message.Text).Should().Equal(Data.Tool.Question, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_A_Run_That_Names_A_Provider_Run_Middleware_Sees_The_Provider()
    {
        // Arrange
        object? provider = null;
        ChatClient.Reply(Text(Data.Tool.Answer));

        Script.Runs[0] = (run, next) =>
        {
            provider = run.Provider;
            return next();
        };

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        provider.Should().Be("scripted");
    }

    [Fact]
    public async Task Given_A_Handoff_To_An_Agent_Of_Another_Provider_Run_Middleware_Sees_Each_Agents_Provider()
    {
        // Arrange
        var providers = new List<object?>();

        ChatClient
            .Reply(Call(Data.Tool.CallId, BackOfficeAgent.Handoff))
            .Reply(Text(Data.Tool.Answer));

        Script.Runs[0] = (run, next) =>
        {
            providers.Add(run.Provider);
            return next();
        };

        // Act
        await Host<FrontDeskAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        providers.Should().Equal(FrontDeskAgent.ModelProvider, BackOfficeAgent.ModelProvider);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_Run_Middleware_That_Adds_A_Message_The_Conversation_Does_Not_Store_It(bool streaming)
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        Script.Runs[0] = (run, next) =>
        {
            run.Messages.Add(new ChatMessage(ChatRole.User, Data.Agent.FollowUp));
            return next();
        };

        // Act
        await RunAsync(Data.Tool.Question, settings: null, streaming);

        // Assert
        (await EntriesAsync(Data.Agent.ConversationId))
            .SelectMany(entry => entry.Messages)
            .Select(message => message.Text)
            .Should()
            .Equal(Data.Tool.Question, Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_Run_Middleware_That_Adds_A_Message_A_Summary_Of_Its_Turns_Leaves_It_In_The_Later_Calls()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "note", ("text", new string('x', Page))))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        Script.Runs[0] = (run, next) =>
        {
            run.Messages.Add(new ChatMessage(ChatRole.User, Data.Agent.FollowUp));
            return next();
        };

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 1_000 });

        // Assert
        ChatClient.Requests[2][1].Text.Should().Be(Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_Run_Middleware_That_Adds_A_Message_The_Summary_Model_Does_Not_Read_It()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "note", ("text", new string('x', Page))))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        Script.Runs[0] = (run, next) =>
        {
            run.Messages.Add(new ChatMessage(ChatRole.User, Data.Agent.FollowUp));
            return next();
        };

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 1_000 });

        // Assert
        ChatClient.Requests[1][1].Text.Should().Be($"user: {Data.Tool.Question}{Environment.NewLine}");
    }

    [Fact]
    public async Task Given_Run_Middleware_That_Replaces_A_Message_The_Summary_Model_Reads_The_Replacement()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "note", ("text", new string('x', Page))))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        Script.Runs[0] = (run, next) =>
        {
            run.Messages[^1] = new ChatMessage(ChatRole.User, Data.Agent.FollowUp);
            return next();
        };

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 1_000 });

        // Assert
        ChatClient.Requests[1][1].Text.Should().Be($"user: {Data.Agent.FollowUp}{Environment.NewLine}");
    }

    [Fact]
    public async Task Given_Run_Middleware_That_Answers_The_Model_Is_Not_Called_And_The_Conversation_Stores_It()
    {
        // Arrange
        Script.Runs[1] = (run, _) =>
        {
            run.Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, Data.Tool.Answer));
            return Task.CompletedTask;
        };

        // Act
        var response = await RunAsync(Data.Tool.Question);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
        ChatClient.Requests.Should().BeEmpty();
        (await EntriesAsync(Data.Agent.ConversationId))
            .SelectMany(entry => entry.Messages)
            .Select(message => message.Text)
            .Should()
            .Equal(Data.Tool.Question, Data.Tool.Answer);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_Middleware_That_Sets_The_Response_After_Next_The_Run_Fails_Saying_What_To_Do_Instead(
        bool streaming)
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        Script.Runs[0] = async (run, next) =>
        {
            await next();
            run.Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, Data.Agent.Reason));
        };

        // Act
        var run = () => RunAsync(Data.Tool.Question, settings: null, streaming);

        // Assert
        await run.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*after next was called*in place of calling next*DelegatingChatClient*");
    }

    [Fact]
    public async Task Given_The_One_Middleware_Setting_The_Response_After_Next_The_Run_Fails_Saying_What_To_Do_Instead()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddTypeshift()
            .WithStore<InMemoryTypeshiftStore<Guid>>()
            .WithChatClientProvider<ScriptedChatClientProvider>()
            .WithRunMiddleware<LateAnswerMiddleware>();

        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var run = () => scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*after next was called*");
    }

    [Fact]
    public async Task Given_An_Outer_Middleware_That_Sets_The_Response_After_An_Inner_One_Answered_The_Run_Fails()
    {
        // Arrange
        Script.Runs[1] = (run, _) => Refuse(run);

        Script.Runs[0] = async (run, next) =>
        {
            await next();
            run.Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, Data.Agent.Reason));
        };

        // Act
        var run = () => RunAsync(Data.Tool.Question);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*after next was called*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Client_That_Redacts_The_Caller_The_Store_And_The_Next_Call_Hold_The_Redacted_Text(
        bool streaming)
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddTypeshift()
            .WithStore<InMemoryTypeshiftStore<Guid>>()
            .WithChatClientProvider<RedactingChatClientProvider>();

        services.AddSingleton(ChatClient);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var chat = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId);

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason));

        // Act
        var response = streaming
            ? (await chat.RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct)).ToChatResponse()
            : await chat.RunAsync(Data.Tool.Question, ct: Ct);

        await chat.RunAsync(Data.Agent.FollowUp, ct: Ct);

        var stored = await provider.GetRequiredService<IConversationStore<Guid>>()
            .LoadConversationAsync(Data.Agent.ConversationId, Ct);

        // Assert
        response.Text.Should().Be(RedactingChatClient.Redacted);
        stored[1].Messages[0].Text.Should().Be(RedactingChatClient.Redacted);
        ChatClient.Requests[1][1].Text.Should().Be(RedactingChatClient.Redacted);
    }

    [Fact]
    public async Task Given_A_Streaming_Run_The_Middleware_Reads_The_Response_Once_The_Stream_Ended()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await StreamAsync(Data.Tool.Question);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
        Script.Log.Should().EndWith($"run 0 after: {Data.Tool.Answer}");
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Middleware_That_Answers_Is_Streamed()
    {
        // Arrange
        Script.Runs[0] = (run, _) =>
        {
            run.Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, Data.Tool.Answer));
            return Task.CompletedTask;
        };

        // Act
        var response = await StreamAsync(Data.Tool.Question);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
        ChatClient.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Left_Early_The_Run_Ends_With_What_Arrived()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer), new TaskCompletionSource(), Text(Data.Agent.FollowUp));

        var updates = Host<ContextAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(Data.Tool.Question, ct: Ct)
            .GetAsyncEnumerator(Ct);

        await updates.MoveNextAsync();

        // Act
        await updates.DisposeAsync().AsTask().WaitAsync(Ct);
        var entries = await EntriesAsync(Data.Agent.ConversationId);

        // Assert
        entries[1].Messages[0].Text.Should().Be(Data.Tool.Answer);
        entries[^1].EndsRun.Should().BeTrue();
    }

    [Fact]
    public async Task Given_Run_Middleware_That_Answers_While_Steered_The_Run_Ends_And_Stores_The_Input()
    {
        // Arrange
        Script.Runs[0] = (run, _) =>
        {
            Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
            run.Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, Data.Tool.Answer));
            return Task.CompletedTask;
        };

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        (await EntriesAsync(Data.Agent.ConversationId))
            .SelectMany(entry => entry.Messages)
            .Select(message => message.Text)
            .Should()
            .Equal(Data.Tool.Question, Data.Tool.Answer, Data.Agent.Steering);
    }

    [Fact]
    public async Task Given_Steering_Input_The_Middleware_Sees_It_Before_The_Model()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.FollowUp));

        var run = RunAsync(Data.Tool.Question);
        await ChatClient.Requested;
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);

        // Act
        gate.SetResult();
        await run;

        // Assert
        Script.Log.Should().Equal(
            "run 0 before",
            "run 1 before",
            $"run 1 after: {Data.Tool.Answer}",
            $"run 0 after: {Data.Tool.Answer}",
            $"steering 0: {Data.Agent.Steering}",
            $"steering 1: {Data.Agent.Steering}",
            "run 0 before",
            "run 1 before",
            $"run 1 after: {Data.Agent.FollowUp}",
            $"run 0 after: {Data.Agent.FollowUp}");
    }

    [Fact]
    public async Task Given_A_Middleware_That_Replaces_Steering_Input_The_Model_And_The_Store_Get_Its_Message()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        Script.Steering[0] = _ => new ChatMessage(ChatRole.User, Data.Agent.CompanyName);

        ChatClient
            .Reply(gate, Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.FollowUp));

        var run = RunAsync(Data.Tool.Question);
        await ChatClient.Requested;
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);

        // Act
        gate.SetResult();
        await run;

        // Assert
        ChatClient.Requests[1].Select(message => message.Text)
            .Should()
            .Equal(Data.Tool.Question, Data.Tool.Answer, Data.Agent.CompanyName);

        (await StoredTextsAsync())
            .Should()
            .Equal(Data.Tool.Question, Data.Tool.Answer, Data.Agent.CompanyName, Data.Agent.FollowUp);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Middleware_That_Drops_Steering_Input_It_Reaches_Neither_The_Model_Nor_The_Store(
        bool streaming)
    {
        // Arrange
        var gate = new TaskCompletionSource();
        Script.Steering[0] = _ => null;

        ChatClient
            .Reply(gate, Call(Data.Tool.CallId, "note", ("text", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        var run = RunAsync(Data.Tool.Question, settings: null, streaming);
        await ChatClient.Requested;
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);

        // Act
        gate.SetResult();
        await run;

        // Assert
        ChatClient.Requests[1].Select(message => message.Text).Should().NotContain(Data.Agent.Steering);
        (await StoredTextsAsync()).Should().NotContain(Data.Agent.Steering);
    }

    [Fact]
    public async Task Given_A_Guardrail_The_Same_Text_Steered_Never_Reaches_The_Model()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        var blocked = $"{Data.Agent.Steering} password";

        Script.Runs[0] = (run, next) => run.Messages[^1].Text.Contains("password") ? Refuse(run) : next();
        Script.Steering[0] = input => input.Text.Contains("password") ? null : input;

        ChatClient.Reply(gate, Text(Data.Tool.Answer));

        var run = RunAsync(Data.Tool.Question);
        await ChatClient.Requested;
        Tracker.TrySteer(Data.Agent.ConversationId, blocked);

        // Act
        gate.SetResult();
        await run;

        // Assert
        ChatClient.Requests.SelectMany(request => request).Select(message => message.Text).Should().NotContain(blocked);
    }

    [Fact]
    public async Task Given_Steering_Input_Dropped_After_The_Answer_The_Run_Ends_Without_Another_Model_Call()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        Script.Steering[0] = _ => null;

        ChatClient.Reply(gate, Text(Data.Tool.Answer));

        var run = RunAsync(Data.Tool.Question);
        await ChatClient.Requested;
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);

        // Act
        gate.SetResult();
        var response = await run;

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
        ChatClient.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Middleware_That_Drops_Steering_Input_A_Run_That_Ends_Before_Taking_It_Does_Not_Store_It()
    {
        // Arrange
        Script.Steering[0] = _ => null;

        Script.Runs[0] = (run, _) =>
        {
            Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
            return Refuse(run);
        };

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        (await StoredTextsAsync()).Should().Equal(Data.Tool.Question, Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Middleware_That_Drops_Steering_Input_After_The_Run_Asked_The_Decision_Stays_Open()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        Script.Steering[0] = _ => null;

        ChatClient.Reply(gate, Call(Data.Tool.CallId, "close_ticket", ("number", Data.Agent.Ticket.Default)));

        var run = RunAsync(Data.Tool.Question);
        await ChatClient.Requested;
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);

        // Act
        gate.SetResult();
        var response = await run;

        // Assert
        response.Decisions.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Middleware_That_Fails_On_Steering_Input_The_Run_Fails_And_The_Input_Is_Not_Stored()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        Script.Steering[0] = _ => throw new InvalidOperationException(Data.Agent.Reason);

        ChatClient.Reply(gate, Text(Data.Tool.Answer));

        var run = RunAsync(Data.Tool.Question);
        await ChatClient.Requested;
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);

        // Act
        gate.SetResult();
        var failed = await Record.ExceptionAsync(() => run);

        // Assert
        failed.Should().BeOfType<InvalidOperationException>();
        (await StoredTextsAsync()).Should().NotContain(Data.Agent.Steering);
    }

    [Fact]
    public async Task Given_Middleware_That_Prepends_A_Message_An_Approval_Still_Runs_The_Stored_Call()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);

        Script.Runs[0] = (run, next) =>
        {
            run.Messages.Insert(0, new ChatMessage(ChatRole.User, Data.Agent.FollowUp));
            return next();
        };

        var approval = DecisionAnswer.Approve(asked.Decisions[0].Id);

        // Act
        await Host<ContextAgent>().WithConversationId(Data.Agent.ConversationId).AnswerAsync([approval], ct: Ct);

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_An_Answering_Run_That_Failed_Before_Its_Approved_Call_Ran_No_Later_Run_Runs_The_Call()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Text(Data.Tool.Answer));

        var chat = Host<ContextAgent>().WithConversationId(Data.Agent.ConversationId);
        var asked = await RunAsync(Data.Tool.Question);
        var approval = DecisionAnswer.Approve(asked.Decisions[0].Id);

        Script.Runs[0] = (_, _) => throw new InvalidOperationException(Data.Agent.Reason);
        var failed = await Record.ExceptionAsync(() => chat.AnswerAsync([approval], ct: Ct));
        Script.Runs.Clear();

        // Act
        await RunAsync(Data.Agent.FollowUp);

        // Assert
        failed.Should().BeOfType<InvalidOperationException>();
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Run_Middleware_That_Neither_Calls_Next_Nor_Answers_The_Run_Fails()
    {
        // Arrange
        Script.Runs[0] = (_, _) => Task.CompletedTask;

        // Act
        var run = () => RunAsync(Data.Tool.Question);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*next*");
    }

    // Answers in place of the model, as a guardrail does
    private Task Refuse(AgentRun run)
    {
        run.Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, Data.Tool.Answer));
        return Task.CompletedTask;
    }

    // The text of each message the conversation stores, in order
    private async Task<List<string>> StoredTextsAsync() =>
    [
        .. (await EntriesAsync(Data.Agent.ConversationId))
            .SelectMany(entry => entry.Messages)
            .Select(message => message.Text)
    ];
}
