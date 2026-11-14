using System.Text.Json;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Conversations.Support;
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
    public async Task Given_A_Conversation_Handed_On_Twice_Its_Next_Run_Goes_To_The_Last_Agent()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_front_agent"))
            .Reply(Call(Data.Tool.SecondCallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason));

        var entry = Host<EntryAgent>().WithConversationId(Data.Agent.ConversationId);
        await entry.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        var response = await entry.RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        Models[^1].Model.Should().Be("back-model");
        response.Messages[^1].AuthorName.Should().Be("back_agent");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Handoff_Whose_Agent_Fails_The_Conversation_Ends_Once_Under_That_Agent(bool streaming)
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(new InvalidOperationException(Data.Agent.Reason));

        // Act
        var run = () => RunAsync(Data.Tool.Question, streaming);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage(Data.Agent.Reason);
        (await EntriesAsync(Data.Agent.ConversationId)).Where(entry => entry.EndsRun)
            .Select(entry => entry.Agent)
            .Should()
            .Equal("back_agent");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_No_Conversation_A_Call_Of_A_Handoff_Is_Answered_As_An_Unknown_Tool(bool streaming)
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await RunAsync(Host<FrontAgent>(), Data.Tool.Question, streaming);

        var result = ChatClient.Requests[1]
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Single()
            .Result?.ToString();

        // Assert
        response.Messages[^1].AuthorName.Should().Be("front_agent");
        result.Should().Contain("not found");
    }

    [Fact]
    public async Task Given_A_Run_Watcher_A_Handoff_Reports_The_First_Agents_Run_As_Ended()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer));

        var changes = Tracker.WatchRunsAsync(Ct).Take(3).ToListAsync(Ct);

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        (await changes).Select(run => (run.Agent, run.Ended))
            .Should()
            .Equal(("front_agent", false), ("front_agent", true), ("back_agent", false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Later_Run_Of_A_Handed_Off_Conversation_It_Loads_It_Once_And_Resolves_Only_Who_Answers(
        bool streaming)
    {
        // Arrange
        var services = new ServiceCollection();
#pragma warning disable TSH036 // the store counts loads and keeps conversations alone, and the run never asks
        services.AddTypeshift().WithStore<CountingLoadStore>().WithChatClientProvider<ScriptedChatClientProvider>();
#pragma warning restore TSH036
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var store = provider.GetRequiredService<CountingLoadStore>();
        var front = scope.ServiceProvider.GetRequiredService<AgentHost<FrontAgent>>()
            .WithConversationId(Data.Agent.ConversationId);

        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason));

        await front.RunAsync(Data.Tool.Question, ct: Ct);
        var loads = store.Loads;
        Models.Clear();

        // Act
        var response = await RunAsync(front, Data.Agent.FollowUp, streaming);

        // Assert
        (store.Loads - loads).Should().Be(1);
        Models.Select(model => model.Model).Should().Equal("back-model");
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
    public async Task Given_Plan_Mode_The_Agent_Is_Offered_No_Handoff()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        var chat = Host<ModeFrontAgent>().WithConversationId(Data.Agent.ConversationId);

        // Act
        await chat.RunAsync(Data.Tool.Question, settings: new() { Mode = AgentMode.Plan }, ct: Ct);

        // Assert
        ChatClient.Options[0]!.Tools!.Select(tool => tool.Name).Should().Equal("exit_plan_mode");
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
    public async Task Given_An_Expired_Decision_Of_The_Agent_Handed_To_An_Answer_Throws_And_Frees_The_Conversation()
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
        var answer = () => Host<GuardedFrontAgent>().WithConversationId(Data.Agent.ConversationId).AnswerAsync(
            [DecisionAnswer.Approve(asked.Decisions.Single().Id)],
            ct: Ct);

        // Assert
        await answer.Should().ThrowAsync<DecisionClosedException>();
        Tracker.Runs.Should().BeEmpty();
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Decision_Of_The_Agent_Handed_To_An_Answer_Through_The_First_Agent_Continues_With_It()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_guarded_back_agent"))
            .Reply(Call(Data.Tool.SecondCallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Text(Data.Tool.Answer));

        var front = Host<GuardedFrontAgent>().WithConversationId(Data.Agent.ConversationId);
        var asked = await front.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        var response = await front.AnswerAsync([DecisionAnswer.Approve(asked.Decisions.Single().Id)], ct: Ct);

        // Assert
        Models[^1].Model.Should().Be("back-model");
        response.Messages[^1].AuthorName.Should().Be("guarded_back_agent");
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_A_Handoff_Called_Next_To_A_Call_That_Waits_For_Approval_It_Is_Refused()
    {
        // Arrange
        var handingBack = $"{Data.Tool.SecondCallId}_back";

        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_guarded_back_agent"))
            .Reply(Calls(
                Call(Data.Tool.SecondCallId, "close_ticket", ("number", Data.Agent.Ticket.Default)),
                Call(handingBack, "handoff_to_guarded_front_agent")))
            .Reply(Text(Data.Tool.Answer));

        var front = Host<GuardedFrontAgent>().WithConversationId(Data.Agent.ConversationId);
        var asked = await front.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await front.AnswerAsync([DecisionAnswer.Approve(asked.Decisions.Single().Id)], ct: Ct);

        var refused = ChatClient.Requests[^1]
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Single(result => result.CallId == handingBack)
            .Result?.ToString();

        // Assert
        asked.Decisions.Single().Agent.Should().Be("guarded_back_agent");
        refused.Should().Be(DecisionResults.NotHandedOff);
        Models[^1].Model.Should().Be("back-model");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Given_A_Tool_Call_Next_To_A_Handoff_The_Agent_Handed_To_Reads_That_It_Did_Not_Run(
        bool streaming,
        bool handoffFirst)
    {
        // Arrange
        var note = Call(Data.Tool.CallId, "note", ("text", Data.Tool.Query));
        var handoff = Call(Data.Tool.SecondCallId, "handoff_to_back_agent");

        ChatClient
            .Reply(handoffFirst ? Calls(handoff, note) : Calls(note, handoff))
            .Reply(Text(Data.Tool.Answer));

        var chat = Host<NotingFrontAgent>().WithConversationId(Data.Agent.ConversationId);

        // Act
        var response = streaming
            ? (await chat.RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct)).ToChatResponse()
            : await chat.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ToolCalls.Should().BeEmpty();
        ResultsOf(ChatClient.Requests[1])[Data.Tool.CallId].Should().StartWith("This call did not run");
        CallsOf(ChatClient.Requests[1]).Should().Contain("note");
        ResultsOf(response.Messages).Keys.Should().BeEquivalentTo(Data.Tool.CallId, Data.Tool.SecondCallId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_Two_Handoff_Calls_In_One_Response_The_Last_One_Hands_Off_And_The_Other_Is_Answered(
        bool streaming)
    {
        // Arrange
        ChatClient
            .Reply(Calls(
                Call(Data.Tool.CallId, "handoff_to_back_agent"),
                Call(Data.Tool.SecondCallId, "handoff_to_guarded_back_agent")))
            .Reply(Text(Data.Tool.Answer));

        var chat = Host<NotingFrontAgent>().WithConversationId(Data.Agent.ConversationId);

        // Act
        var response = streaming
            ? (await chat.RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct)).ToChatResponse()
            : await chat.RunAsync(Data.Tool.Question, ct: Ct);

        var results = ResultsOf(ChatClient.Requests[1]);

        // Assert
        response.Messages[^1].AuthorName.Should().Be("guarded_back_agent");
        results[Data.Tool.CallId].Should().Contain("handed off to guarded_back_agent");
        results[Data.Tool.SecondCallId].Should().Be("Handed off to guarded_back_agent.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_Two_Agents_Handing_Back_And_Forth_The_Run_Ends_After_The_Most_Handoffs(bool streaming)
    {
        // Arrange
        ScriptHandoffsBackAndForth(11);
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await RunAsync(Data.Tool.Question, streaming);
        var refused = ResultsOf(ChatClient.Requests[^1])[$"{Data.Tool.CallId}_10"];

        // Assert
        ChatClient.Requests.Should().HaveCount(12);
        refused.Should().Be("No more handoffs in this run: answer the user yourself.");
        response.Messages[^1].AuthorName.Should().Be("front_agent");
        response.Text.Should().EndWith(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Model_That_Hands_Off_Whatever_It_Reads_The_Run_Ends_At_Its_Agents_Limit_Of_Model_Calls()
    {
        // Arrange
        ScriptHandoffsBackAndForth(60);

        // Act
        await RunAsync(Data.Tool.Question);
        var entries = await EntriesAsync(Data.Agent.ConversationId);

        // Assert
        ChatClient.Requests.Should().HaveCount(51);
        entries[^1].EndsRun.Should().BeTrue();
        Tracker.Runs.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Conversation_Handed_Off_In_Auto_Mode_A_Run_Naming_The_Default_Mode_Asks_Again()
    {
        // Arrange
        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Call(Data.Tool.CallId, "handoff_to_guarded_back_agent"))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Call(Data.Tool.SecondCallId, "close_ticket", ("number", Data.Agent.Ticket.Default)));

        var chat = Host<ModeFrontAgent>().WithConversationId(Data.Agent.ConversationId);

        await chat.RunAsync(Data.Tool.Question, settings: new() { Mode = AgentMode.Auto }, ct: Ct);
        await chat.RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Act
        var response = await chat.RunAsync(Data.Agent.Steering, settings: new() { Mode = AgentMode.Default }, ct: Ct);

        // Assert
        Models[^1].Model.Should().Be("back-model");
        response.Decisions.Should().ContainSingle();
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Conversation_Handed_Off_A_Run_Naming_Plan_Mode_Offers_Only_What_Plan_Mode_Offers()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_guarded_back_agent"))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        var chat = Host<ModeFrontAgent>().WithConversationId(Data.Agent.ConversationId);
        await chat.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await chat.RunAsync(Data.Agent.FollowUp, settings: new() { Mode = AgentMode.Plan }, ct: Ct);

        // Assert
        Models[^1].Model.Should().Be("back-model");
        ChatClient.Options[2]!.Tools!.Select(tool => tool.Name).Should().Equal("exit_plan_mode");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Run_Naming_A_Mode_That_Hands_Off_The_Agent_Handed_To_Runs_In_It(bool streaming)
    {
        // Arrange
        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Call(Data.Tool.CallId, "handoff_to_guarded_back_agent"))
            .Reply(Call(Data.Tool.SecondCallId, "close_ticket", ("number", Data.Agent.Ticket.Default)));

        var chat = Host<ModeFrontAgent>().WithConversationId(Data.Agent.ConversationId);
        var named = new ModeFrontAgent.Settings { Mode = AgentMode.Default };

        await chat.RunAsync(Data.Tool.Question, settings: new() { Mode = AgentMode.Auto }, ct: Ct);

        // Act
        var response = streaming
            ? (await chat.RunStreamingAsync(Data.Agent.FollowUp, named, Ct).ToListAsync(Ct)).ToChatResponse()
            : await chat.RunAsync(Data.Agent.FollowUp, named, Ct);

        // Assert
        response.Decisions.Single().Agent.Should().Be("guarded_back_agent");
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Plan_Approved_Into_Auto_Mode_Then_A_Handoff_The_Agent_Handed_To_Runs_In_Auto()
    {
        // Arrange
        ChatClient
            .Reply(Call($"{Data.Tool.CallId}_plan", "exit_plan_mode", ("plan", Data.Tool.Answer)))
            .Reply(Call(Data.Tool.CallId, "handoff_to_guarded_back_agent"))
            .Reply(Call(Data.Tool.SecondCallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Text(Data.Tool.Answer));

        var chat = Host<ModeFrontAgent>().WithConversationId(Data.Agent.ConversationId);
        var planned = await chat.RunAsync(Data.Tool.Question, settings: new() { Mode = AgentMode.Plan }, ct: Ct);

        // Act
        var response = await chat.AnswerAsync(
            [planned.Decisions.OfType<PlanDecision>().Single().Approve(AgentMode.Auto)],
            ct: Ct);

        // Assert
        response.Decisions.Should().BeEmpty();
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
        ChatClient.Options[2]!.Tools!.Select(tool => tool.Name).Should().NotContain("exit_plan_mode");
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

    // The front and the back agent hand the conversation to each other, the front agent first
    private void ScriptHandoffsBackAndForth(int handoffs)
    {
        foreach (var hop in Enumerable.Range(0, handoffs))
        {
            ChatClient.Reply(Call(
                $"{Data.Tool.CallId}_{hop}",
                hop % 2 == 0 ? "handoff_to_back_agent" : "handoff_to_front_agent"));
        }
    }

    // The texts of the tool results among the messages, by call id
    private static Dictionary<string, string> ResultsOf(IEnumerable<ChatMessage> messages) =>
        messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .ToDictionary(result => result.CallId, result => result.Result?.ToString() ?? "");

    // The names of the tools the messages hold calls of
    private static IEnumerable<string> CallsOf(IEnumerable<ChatMessage> messages) =>
        messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().Select(call => call.Name);

    private Task<ChatResponse> RunAsync(string input) =>
        Host<FrontAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(input, ct: Ct);

    private Task<ChatResponse> RunAsync(string input, bool streaming) =>
        RunAsync(Host<FrontAgent>().WithConversationId(Data.Agent.ConversationId), input, streaming);

    // The same run on either path, a streamed one as the response its updates make
    private async Task<ChatResponse> RunAsync(AgentHost<FrontAgent> front, string input, bool streaming) =>
        streaming
            ? (await front.RunStreamingAsync(input, ct: Ct).ToListAsync(Ct)).ToChatResponse()
            : await front.RunAsync(input, ct: Ct);
}
