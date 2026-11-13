using System.Text.Json;
using Typeshift.Tests.MultiAgent.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.MultiAgent;

public class HandoffTests : MultiAgentTestBase
{
    [Fact]
    public async Task Given_A_Handoff_The_Agent_Handed_To_Answers_In_The_Same_Run_With_The_Whole_Conversation()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await RunAsync(Data.Tool.Question);

        // Assert
        response.Messages[^1].AuthorName.Should().Be("back_agent");
        response.Text.Should().EndWith(Data.Tool.Answer);
        ChatClient.Options[1]!.Instructions.Should().Be(BackAgent.Prompt);
        ChatClient.Requests[1][0].Text.Should().Be(Data.Tool.Question);
        ChatClient.Requests[1].SelectMany(message => message.Contents).OfType<FunctionResultContent>()
            .Should()
            .ContainSingle(result => result.CallId == Data.Tool.CallId);
    }

    [Fact]
    public async Task Given_A_Conversation_Handed_Off_Its_Next_Run_Goes_To_The_Agent_That_Answered()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason));

        await RunAsync(Data.Tool.Question);

        // Act
        var response = await RunAsync(Data.Agent.FollowUp);

        // Assert
        Models[^1].Model.Should().Be("back-model");
        response.Messages[^1].AuthorName.Should().Be("back_agent");
    }

    [Fact]
    public async Task Given_A_Run_Starting_While_A_Handoff_Passes_On_The_Conversation_The_Agent_Handed_To_Answers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<InMemoryTypeshiftStore<Guid>>().WithChatClientProvider<GatedBackProvider>();
        services.AddSingleton(ChatClient).AddSingleton<ProviderGate>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var front = scope.ServiceProvider.GetRequiredService<AgentHost<FrontAgent>>();
        var gate = provider.GetRequiredService<ProviderGate>();

        var answering = new TaskCompletionSource();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(answering, Text(Data.Tool.Answer));

        var first = front.WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);
        await gate.Holding.Task.WaitAsync(Ct);

        // Act
        var second = front.WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Agent.FollowUp, ct: Ct);
        gate.Release.SetResult();

        var settled = await Task.WhenAny(first, second);
        answering.SetResult();

        // Assert
        settled.Should().BeSameAs(second);
        await FluentActions.Awaiting(() => second).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TrySteer*");
        (await first).Messages[^1].AuthorName.Should().Be("back_agent");
    }

    [Fact]
    public async Task Given_A_Run_Starting_While_A_Streamed_Handoff_Passes_On_The_Conversation_The_Agent_Answers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<InMemoryTypeshiftStore<Guid>>().WithChatClientProvider<GatedBackProvider>();
        services.AddSingleton(ChatClient).AddSingleton<ProviderGate>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var front = scope.ServiceProvider.GetRequiredService<AgentHost<FrontAgent>>();
        var gate = provider.GetRequiredService<ProviderGate>();

        var answering = new TaskCompletionSource();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(answering, Text(Data.Tool.Answer));

        var first = front
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(Data.Tool.Question, ct: Ct)
            .ToListAsync(Ct)
            .AsTask();

        await gate.Holding.Task.WaitAsync(Ct);

        // Act
        var second = front.WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Agent.FollowUp, ct: Ct);
        gate.Release.SetResult();

        var settled = await Task.WhenAny(first, second);
        answering.SetResult();

        // Assert
        settled.Should().BeSameAs(second);
        await FluentActions.Awaiting(() => second).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TrySteer*");
        (await first).ToChatResponse().Messages[^1].AuthorName.Should().Be("back_agent");
    }

    [Fact]
    public async Task Given_A_Handoff_Whose_Agent_Fails_Before_It_Begins_A_Run_Waiting_For_The_Conversation_Runs()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<InMemoryTypeshiftStore<Guid>>().WithChatClientProvider<GatedBackProvider>();
        services.AddSingleton(ChatClient).AddSingleton<ProviderGate>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var front = scope.ServiceProvider.GetRequiredService<AgentHost<FrontAgent>>();
        var gate = provider.GetRequiredService<ProviderGate>();
        gate.Failure = new InvalidOperationException(Data.Agent.Reason);

        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer));

        var first = front.WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);
        await gate.Holding.Task.WaitAsync(Ct);

        // Act
        var second = front.WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Agent.FollowUp, ct: Ct);
        gate.Release.SetResult();

        // Assert
        await FluentActions.Awaiting(() => first).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(Data.Agent.Reason);
        (await second).Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Handoff_Back_To_The_First_Agent_In_The_Same_Run_The_First_Agent_Answers()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Call(Data.Tool.SecondCallId, "handoff_to_front_agent"))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await RunAsync(Data.Tool.Question);

        // Assert
        response.Messages[^1].AuthorName.Should().Be("front_agent");
        Tracker.Runs.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Handoff_To_An_Agent_It_Does_Not_Know_The_Run_Goes_On_And_Ends()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_nobody"))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await RunAsync(Data.Tool.Question);

        // Assert
        response.Text.Should().EndWith(Data.Tool.Answer);
        (await EntriesAsync(Data.Agent.ConversationId))[^1].EndsRun.Should().BeTrue();
    }

    [Fact]
    public async Task Given_No_Conversation_Handoffs_Are_Not_Offered()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<FrontAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ChatClient.Options[0]!.Tools.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Given_A_Streaming_Run_A_Handoff_Streams_The_Answer_Of_The_Agent_Handed_To()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var updates = await Host<FrontAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(Data.Tool.Question, ct: Ct)
            .ToListAsync(Ct);

        // Assert
        updates[^1].AuthorName.Should().Be("back_agent");
        updates.ToChatResponse().Text.Should().EndWith(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_Steering_Input_Waiting_At_A_Handoff_The_Agent_Handed_To_Answers_It()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer));

        var run = RunAsync(Data.Tool.Question);
        await ChatClient.Requested;

        // Act
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        gate.SetResult();
        var response = await run;

        // Assert
        response.Messages[^1].AuthorName.Should().Be("back_agent");
        ChatClient.Requests[1].Select(message => message.Text).Should().Contain(Data.Agent.Steering);
        ChatClient.Requests[1][^1].Text.Should().Be(Data.Agent.Steering);
    }

    [Fact]
    public async Task Given_A_Closed_Request_Of_The_Agent_Handed_To_An_Answer_Throws_And_Frees_The_Conversation()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_guarded_back_agent"))
            .Reply(Call(Data.Tool.SecondCallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Text(Data.Tool.Answer));

        var asked = await Host<GuardedFrontAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        await Host<GuardedFrontAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Act
        var answer = () => Host<GuardedFrontAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(
            new ChatMessage(ChatRole.User, [asked.ApprovalRequests.Single().CreateResponse(approved: true)]),
            ct: Ct);

        // Assert
        await answer.Should().ThrowAsync<ApprovalClosedException>();
        Tracker.Runs.Should().BeEmpty();
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Typed_Run_Handed_Off_The_Agent_Handed_To_Answers_In_Its_Format()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Lives()));

        // Act
        var response = await Host<FrontAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync<int>(Data.Tool.Question, ct: Ct);

        // Assert
        ChatClient.Options[1]!.ResponseFormat.Should().BeOfType<ChatResponseFormatJson>();
        response.Result.Should().Be(Data.Tool.Lives);
    }

    [Fact]
    public async Task Given_A_Typed_Streaming_Run_Handed_Off_The_Agent_Handed_To_Answers_In_Its_Format()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Lives()));

        var run = Host<FrontAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync<int>(Data.Tool.Question, ct: Ct);

        // Act
        await run.ToListAsync(Ct);

        // Assert
        ChatClient.Options[1]!.ResponseFormat.Should().BeOfType<ChatResponseFormatJson>();
        run.Response.Result.Should().Be(Data.Tool.Lives);
    }

    [Fact]
    public async Task Given_A_Conversation_Handed_Off_A_Typed_Run_Asks_The_Agent_That_Answered_For_Its_Format()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Lives()));

        await RunAsync(Data.Tool.Question);

        // Act
        var response = await Host<FrontAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync<int>(Data.Agent.FollowUp, ct: Ct);

        // Assert
        ChatClient.Options[2]!.ResponseFormat.Should().BeOfType<ChatResponseFormatJson>();
        response.Result.Should().Be(Data.Tool.Lives);
    }

    [Fact]
    public async Task Given_An_Untyped_Run_Handed_Off_The_Agent_Handed_To_Answers_In_Text()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        ChatClient.Options[1]!.ResponseFormat.Should().BeNull();
    }

    [Fact]
    public async Task Given_The_Own_Settings_Of_The_Agent_Handed_To_It_Answers_With_Them()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer));

        await Host<BackAgent>().ConfigureAsync(settings => settings with { Temperature = Data.Agent.Temperature }, Ct);

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        ChatClient.Options[1]!.Temperature.Should().Be(Data.Agent.Temperature);
    }

    private string Lives() => JsonSerializer.Serialize(new { data = Data.Tool.Lives }, Json.SerializerOptions);

    private Task<ChatResponse> RunAsync(string input) =>
        Host<FrontAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(input, ct: Ct);
}
