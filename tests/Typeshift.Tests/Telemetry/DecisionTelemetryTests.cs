using System.Diagnostics;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Decisions.Support;
using Typeshift.Tests.Telemetry.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Telemetry;

public sealed class DecisionTelemetryTests : QuestionTestBase
{
    private const string Asked = "typeshift.decision.asked";

    private const string Settled = "typeshift.decision.settled";

    private const string Outcome = "typeshift.decision.outcome";

    // One listener for every test of the class, listening before the first test runs; each test finds its runs by
    // its conversation
    private static readonly RecordedActivities Activities;

    static DecisionTelemetryTests() => Activities = new RecordedActivities();

    [Fact]
    public async Task Given_A_Run_That_Asks_Its_Span_Counts_The_Decisions_And_Carries_An_Event_For_Each()
    {
        // Arrange
        ChatClient.Reply(Calls(
            Ask(Data.Tool.CallId),
            CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket),
            Note($"{Data.Tool.CallId}_note", Data.Tool.Query)));

        // Act
        var response = await Asking.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        run.GetTagItem("typeshift.decisions.asked").Should().Be(2);
        Tags(run, Asked, "typeshift.decision.kind").Should().Equal("question", "approval");
        Tags(run, Asked, "typeshift.decision.id").Should().Equal(response.Decisions.Select(decision => decision.Id));
        Tags(run, Asked, "gen_ai.tool.name").Should().Equal("ask_user", "close_ticket");
        run.Status.Should().NotBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task Given_No_Message_Capture_The_Event_Of_A_Decision_Asked_Leaves_The_Calls_Arguments_Out()
    {
        // Arrange
        await CaptureMessagesAsync(false);
        ChatClient.Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket));

        // Act
        await Asking.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        var asked = Activities.Run(Data.Agent.ConversationId).Events.Single(@event => @event.Name == Asked);
        asked.Tags.Select(tag => tag.Key).Should().NotContain("gen_ai.tool.call.arguments");
    }

    [Fact]
    public async Task Given_Message_Capture_The_Event_Of_A_Decision_Asked_Carries_The_Calls_Arguments()
    {
        // Arrange
        await CaptureMessagesAsync(true);
        ChatClient.Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket));

        // Act
        await Asking.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Tags(Activities.Run(Data.Agent.ConversationId), Asked, "gen_ai.tool.call.arguments")
            .Should()
            .Equal($$"""{"number":{{Data.Agent.Ticket.Default}}}""");
    }

    [Fact]
    public async Task Given_Answers_Carried_Out_The_Continuing_Runs_Span_Names_What_Became_Of_Each_Call()
    {
        // Arrange
        ChatClient
            .Reply(Calls(
                Ask(Data.Tool.CallId),
                CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket),
                CloseTicket($"{Data.Tool.SecondCallId}_too", Data.Agent.Ticket.Updated),
                Note($"{Data.Tool.CallId}_note", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        var asked = await Asking.RunAsync(Data.Tool.Question, ct: Ct);
        var question = (QuestionDecision)asked.Decisions[0];

        // Act
        await AnswerQuestionsAsync(
            question.Answer(Data.Questions.Label.Default),
            DecisionAnswer.Approve(asked.Decisions[1].Id),
            asked.Decisions[2].Reject(Data.Agent.Reason));

        // Assert
        var continued = Activities.Runs(Data.Agent.ConversationId)[^1];
        Tags(continued, Settled, Outcome).Should().Equal("answered", "approved", "rejected", "run");
        Tags(continued, Settled, "gen_ai.tool.call.result").Should().BeEmpty();
        continued.GetTagItem("typeshift.decisions.asked").Should().BeNull();
    }

    [Fact]
    public async Task Given_Message_Capture_The_Event_Of_A_Rejected_Call_Carries_What_The_Model_Reads_For_It()
    {
        // Arrange
        await CaptureMessagesAsync(true);

        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var asked = await Asking.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await AnswerQuestionsAsync(asked.Decisions[0].Reject(Data.Agent.Reason));

        // Assert
        Tags(Activities.Runs(Data.Agent.ConversationId)[^1], Settled, "gen_ai.tool.call.result")
            .Should()
            .Equal($"The user rejected this call. {Data.Agent.Reason}");
    }

    [Fact]
    public async Task Given_Declined_Questions_The_Continuing_Runs_Span_Names_Them_Declined()
    {
        // Arrange
        var asked = await AskAsync();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await AnswerQuestionsAsync(asked.Reject());

        // Assert
        Tags(Activities.Runs(Data.Agent.ConversationId)[^1], Settled, Outcome).Should().Equal("declined");
    }

    [Fact]
    public async Task Given_A_New_Message_In_Place_Of_An_Answer_The_Next_Runs_Span_Names_Each_Call_Expired()
    {
        // Arrange
        var note = Note(Data.Tool.SecondCallId, Data.Tool.Query);

        ChatClient
            .Reply(Calls(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket), note))
            .Reply(Text(Data.Tool.Answer));

        await Asking.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await Asking.RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        Tags(Activities.Runs(Data.Agent.ConversationId)[^1], Settled, Outcome).Should().Equal("expired", "expired");
    }

    [Fact]
    public async Task Given_A_Batch_A_Stopped_Run_Started_The_Next_Runs_Span_Names_Its_Approved_Call_Unknown()
    {
        // Arrange
        ChatClient
            .Reply(Calls(Ask(Data.Tool.CallId), CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket)))
            .Reply(Text(Data.Tool.Answer));

        var asked = await Asking.RunAsync(Data.Tool.Question, ct: Ct);
        var question = (QuestionDecision)asked.Decisions[0];
        var approval = DecisionAnswer.Approve(asked.Decisions[1].Id);

        await Asking.RecordAnswersAsync([question.Answer(Data.Questions.Label.Default), approval], Ct);
        await StartAsync();

        // Act
        await Asking.RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        Tags(Activities.Runs(Data.Agent.ConversationId)[^1], Settled, Outcome).Should().Equal("answered", "unknown");
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Steering_After_The_Run_Asked_Its_Own_Span_Names_The_Call_Asked_And_Then_Expired()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var running = Asking.RunAsync(Data.Tool.Question, ct: Ct);
        await ChatClient.Requested.WaitAsync(Ct);
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);

        // Act
        gate.SetResult();
        await running;

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        run.GetTagItem("typeshift.decisions.asked").Should().Be(1);
        Tags(run, Settled, Outcome).Should().Equal("expired");
    }

    [Fact]
    public async Task Given_A_Store_That_Fails_To_Save_What_A_Run_Asks_The_Runs_Span_Carries_The_Error()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddTypeshift()
            .WithStore(new UnreliableStore { FailingSave = 1 })
            .WithChatClientProvider<ScriptedChatClientProvider>();

        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        ChatClient.Reply(Ask(Data.Tool.CallId));

        var host = scope.ServiceProvider.GetRequiredService<AgentHost<AskingAgent>>()
            .WithConversationId(Data.Agent.ConversationId);

        // Act
        var failure = await Record.ExceptionAsync(() => host.RunAsync(Data.Tool.Question, ct: Ct));

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        failure.Should().BeOfType<IOException>();
        run.Status.Should().Be(ActivityStatusCode.Error);
        run.GetTagItem("typeshift.decisions.asked").Should().BeNull();
    }

    // The values of one tag across a span's events of a name, in the order the events were added
    private static IEnumerable<object?> Tags(Activity run, string @event, string tag) =>
        run.Events
            .Where(added => added.Name == @event)
            .SelectMany(added => added.Tags.Where(pair => pair.Key == tag))
            .Select(pair => pair.Value);

    // A run began to carry the answers out and stopped before any result was stored
    private async Task StartAsync()
    {
        var ledger = new DecisionLedger(Services.GetRequiredService<StoreAccess>(), Time);
        var conversation = new StoreId<Guid>(Data.Agent.ConversationId);
        var answered = await ledger.LoadAsync(conversation, Ct);

        await ledger.StartAsync(conversation, answered.Batch!, Ct);
    }

    // Capture as OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT sets it for a process, here for this test's host
    private async Task CaptureMessagesAsync(bool capture)
    {
        var host = Host<AskingAgent>();
        var options = await host.ResolveOptionsAsync(ct: Ct);
        using var client = await host.CreateChatClientAsync(options, Ct);

        client.GetService<OpenTelemetryChatClient>()!.EnableSensitiveData = capture;
    }
}
