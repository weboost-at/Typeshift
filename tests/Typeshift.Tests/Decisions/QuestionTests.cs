using System.Text.Json;
using Typeshift.Tests.Approval.Support;
using Typeshift.Tests.Decisions.Support;
using Typeshift.Tests.Testing.Fakes;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Decisions;

public class QuestionTests : QuestionTestBase
{
    private const string NotAvailable =
        "ask_user is not available here: go on without asking, and say in your answer what you need to know.";

    private const string CallAgain = "Correct the arguments and call ask_user again.";

    [Fact]
    public async Task Given_An_Agent_That_Declares_Questions_Ask_User_Is_Offered_After_Its_Tools()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Asking.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        OfferedTo(ChatClient, 0).Should().Equal("close_ticket", "read_ticket", "note", "ask_user");
    }

    [Fact]
    public async Task Given_An_Agent_That_Declares_No_Questions_Ask_User_Is_Not_Offered()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        OfferedTo(ChatClient, 0).Should().NotContain("ask_user");
    }

    [Fact]
    public async Task Given_No_Conversation_Ask_User_Is_Not_Offered_And_A_Call_Of_It_Is_Answered_As_Not_Available()
    {
        // Arrange
        ChatClient
            .Reply(Ask(Data.Tool.CallId))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<AskingAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        OfferedTo(ChatClient, 0).Should().NotContain("ask_user");
        ResultSentToModel(1).Should().Be(NotAvailable);
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Sub_Agent_That_Declares_Questions_It_Is_Not_Offered_Ask_User_And_Its_Call_Asks_Nobody()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "asking_helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient
            .Reply(Ask(Data.Tool.SecondCallId))
            .Reply(Text(Data.Agent.Reason));

        // Act
        var response = await Host<AskingLeadAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        OfferedTo(HelperClient, 0).Should().BeEmpty();
        ResultsSentTo(HelperClient, 1).Should().Equal(NotAvailable);
        response.Decisions.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Background_Sub_Agent_That_Declares_Questions_Its_Call_Of_Ask_User_Asks_Nobody()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "asking_helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Agent.Steering))
            .Reply(Text(Data.Tool.Answer));

        HelperClient
            .Reply(gate, Ask(Data.Tool.SecondCallId))
            .Reply(Text(Data.Agent.Reason));

        var running = Host<BackgroundAskingLeadAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        await ChatClient.RequestedAsync(2);

        // Act
        gate.SetResult();
        var response = await running;

        // Assert
        OfferedTo(HelperClient, 0).Should().BeEmpty();
        ResultsSentTo(HelperClient, 1).Should().Equal(NotAvailable);
        response.Decisions.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Questions_The_Run_Ends_With_One_Decision_That_Carries_Them()
    {
        // Arrange
        ChatClient.Reply(Ask(Data.Tool.CallId, Question(multiSelect: true), SecondQuestion()));

        // Act
        var response = await Asking.RunAsync(Data.Tool.Question, ct: Ct);
        var asked = (QuestionDecision)response.Decisions.Single();

        // Assert
        asked.Agent.Should().Be("asking_agent");
        asked.Questions.Select(question => question.Header).Should().Equal(Header, Data.Questions.SecondHeader);
        asked.Questions[0].Text.Should().Be(Data.Questions.Text);
        asked.Questions[0].Options.Select(option => option.Label).Should().Equal(Data.Questions.Labels);
        asked.Questions[0].Options[0].Description.Should().Be($"Go with {Label.Default}.");
        asked.Questions.Select(question => question.MultiSelect).Should().Equal(true, false);
    }

    [Fact]
    public async Task Given_Answers_The_Model_Reads_Them_As_The_Calls_Result_And_The_Run_Goes_On()
    {
        // Arrange
        var asked = await AskAsync(Question(), SecondQuestion());
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await AnswerQuestionsAsync(asked.Answer(new QuestionAnswer([Label]), Data.Questions.OwnWords));

        // Assert
        ResultSentToModel(1).Should().Be(
            $$"""{"answers":[{"header":"{{Header}}","selected":["{{Label.Default}}"]},"""
            + $$"""{"header":"{{Data.Questions.SecondHeader}}","text":"{{Data.Questions.OwnWords}}"}]}""");
        response.Text.Should().Be(Data.Tool.Answer);
        response.Decisions.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_An_Answer_In_Words_That_Are_An_Options_Label_It_Selects_The_Option()
    {
        // Arrange
        var asked = await AskAsync();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await AnswerQuestionsAsync(asked.Answer(Label.Updated));

        // Assert
        ResultSentToModel(1).Should().Be(Selected(Label.Updated));
    }

    [Fact]
    public async Task Given_A_Question_That_Takes_Several_Options_An_Answer_Selects_Several_And_Adds_Words()
    {
        // Arrange
        var asked = await AskAsync(Question(multiSelect: true));
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await AnswerQuestionsAsync(asked.Answer(new QuestionAnswer([Label, Label.Updated], Data.Questions.OwnWords)));
        var selected = $$"""["{{Label.Default}}","{{Label.Updated}}"]""";

        // Assert
        ResultSentToModel(1).Should().Be(
            $$"""{"answers":[{"header":"{{Header}}","selected":{{selected}},"text":"{{Data.Questions.OwnWords}}"}]}""");
    }

    [Fact]
    public async Task Given_Declined_Questions_The_Model_Reads_That_The_User_Declined_And_Why()
    {
        // Arrange
        var asked = await AskAsync();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await AnswerQuestionsAsync(asked.Reject(Data.Agent.Reason));

        // Assert
        ResultSentToModel(1).Should().Be($"The user declined to answer. {Data.Agent.Reason}");
    }

    [Theory]
    [InlineData("""{}""", "questions must be an array of one to four questions.")]
    [InlineData("""{"questions":[]}""", "questions must hold one to four questions, and it holds 0.")]
    [InlineData(
        """
        {"questions":[{"question":"Which?","header":"Pick","options":[{"label":"A","description":"a"}]}]}
        """,
        "questions[0].options must hold two to four options, and it holds 1.")]
    [InlineData(
        """
        {"questions":[{"question":"Which?","options":[
          {"label":"A","description":"a"},{"label":"B","description":"b"}]}]}
        """,
        "questions[0].header must be a text that is not empty.")]
    [InlineData(
        """
        {"questions":[{"question":"Which?","header":"Pick","options":[
          {"label":"A","description":"a"},{"label":"A","description":"b"}]}]}
        """,
        "questions[0].options holds the label 'A' twice, and the user answers by label.")]
    [InlineData(
        """
        {"questions":[{"question":"Which?","header":"Pick","options":[
          {"label":"A","description":"a"},{"label":"B"}]}]}
        """,
        "questions[0].options[1].description must be a text that is not empty.")]
    [InlineData(
        """
        {"questions":[{"question":"Which?","header":"Pick","multiSelect":"yes","options":[
          {"label":"A","description":"a"},{"label":"B","description":"b"}]}]}
        """,
        "questions[0].multiSelect must be true or false.")]
    public async Task Given_Arguments_That_Break_The_Tools_Shape_Nobody_Is_Asked_And_The_Model_Reads_What_Is_Wrong(
        string arguments,
        string wrong)
    {
        // Arrange
        ChatClient
            .Reply(Malformed(Data.Tool.CallId, arguments))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Asking.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Decisions.Should().BeEmpty();
        ResultSentToModel(1).Should().Be($"The user was not asked: {wrong} {CallAgain}");
        (await StoredDecisionsAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Given_Five_Questions_Nobody_Is_Asked()
    {
        // Arrange
        ChatClient
            .Reply(Ask(Data.Tool.CallId, [.. Enumerable.Repeat(Question(), 5)]))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Asking.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Decisions.Should().BeEmpty();
        ResultSentToModel(1).Should().Contain("questions must hold one to four questions, and it holds 5.");
    }

    [Fact]
    public async Task Given_Malformed_Questions_Next_To_A_Call_That_Waits_The_Model_Reads_What_Is_Wrong_Once_Answered()
    {
        // Arrange
        ChatClient
            .Reply(Calls(Malformed(Data.Tool.CallId, "{}"), CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket)))
            .Reply(Text(Data.Tool.Answer));

        var asked = await Asking.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await AnswerQuestionsAsync(asked.Decisions.OfType<ApprovalDecision>().Single().Approve());

        // Assert
        asked.Decisions.Should().ContainSingle();
        ResultsSentToModel(1).Should().Contain(result => result.StartsWith("The user was not asked:"));
    }

    [Fact]
    public async Task Given_Fewer_Answers_Than_Questions_The_Answer_Throws_And_The_Decision_Stays_Open()
    {
        // Arrange
        var asked = await AskAsync(Question(), SecondQuestion());

        // Act
        var answering = () => AnswerQuestionsAsync(asked.Answer(Label.Default));

        // Assert
        await answering.Should().ThrowAsync<ArgumentException>().WithMessage("*1 answers for 2 questions*");
        (await Asking.GetDecisionsAsync(Ct)).Open.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Selected_Label_The_Question_Does_Not_Offer_The_Answer_Throws_Naming_It()
    {
        // Arrange
        var asked = await AskAsync();

        // Act
        var answering = () => AnswerQuestionsAsync(asked.Answer(new QuestionAnswer([Label.Invalid])));

        // Assert
        await answering.Should().ThrowAsync<ArgumentException>().WithMessage($"*'{Label.Invalid}'*'{Header}'*");
    }

    [Fact]
    public async Task Given_Two_Options_Selected_For_A_Question_That_Takes_One_The_Answer_Throws()
    {
        // Arrange
        var asked = await AskAsync();

        // Act
        var answering = () => AnswerQuestionsAsync(asked.Answer(new QuestionAnswer([Label, Label.Updated])));

        // Assert
        await answering.Should().ThrowAsync<ArgumentException>().WithMessage("*MultiSelect*");
    }

    [Fact]
    public async Task Given_An_Answer_That_Selects_Nothing_And_Says_Nothing_It_Throws()
    {
        // Arrange
        var asked = await AskAsync();

        // Act
        var answering = () => AnswerQuestionsAsync(asked.Answer(new QuestionAnswer([])));

        // Assert
        await answering.Should().ThrowAsync<ArgumentException>().WithMessage("*unanswered*");
    }

    [Fact]
    public async Task Given_An_Approval_For_Questions_The_Answer_Throws_Naming_Answer_And_Reject()
    {
        // Arrange
        var asked = await AskAsync();

        // Act
        var answering = () => AnswerQuestionsAsync(DecisionAnswer.Approve(asked.Id));

        // Assert
        await answering.Should().ThrowAsync<ArgumentException>().WithMessage("*Answer(...)*Reject()*");
    }

    [Fact]
    public async Task Given_Answers_For_A_Tool_Call_The_Answer_Throws_And_The_Call_Does_Not_Run()
    {
        // Arrange
        ChatClient.Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket));
        var asked = await Asking.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        var answering = () => AnswerQuestionsAsync(DecisionAnswer.Answer(asked.Decisions[0].Id, Label.Default));

        // Assert
        await answering.Should().ThrowAsync<ArgumentException>().WithMessage("*asks none*");
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_The_Same_Answers_Recorded_Twice_The_Second_Changes_Nothing()
    {
        // Arrange
        var asked = await AskAsync();
        await Asking.RecordAnswersAsync([asked.Answer(Label.Default)], Ct);

        // Act
        var decisions = await Asking.RecordAnswersAsync([asked.Answer(new QuestionAnswer([Label]))], Ct);

        // Assert
        decisions.CanResume.Should().BeTrue();
    }

    [Fact]
    public async Task Given_Other_Answers_After_The_First_They_Throw_Naming_The_Decision_Answered()
    {
        // Arrange
        var asked = await AskAsync();
        await Asking.RecordAnswersAsync([asked.Answer(Label.Default)], Ct);

        // Act
        var answering = () => Asking.RecordAnswersAsync([asked.Answer(Label.Updated)], Ct);

        // Assert
        await answering.Should()
            .ThrowAsync<DecisionClosedException>()
            .Where(closed => closed.Decisions[asked.Id] == DecisionClosedReason.Answered);
    }

    [Fact]
    public async Task Given_Plan_Mode_Ask_User_Is_Offered_Beside_The_Read_Only_Tools()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Asking.RunAsync(Data.Tool.Question, settings: new() { Mode = AgentMode.Plan }, ct: Ct);

        // Assert
        OfferedTo(ChatClient, 0).Should().Equal("read_ticket", "ask_user", "exit_plan_mode");
    }

    [Fact]
    public async Task Given_Plan_Mode_A_Question_Is_Asked_And_Answered_Before_The_Plan()
    {
        // Arrange
        ChatClient
            .Reply(Ask(Data.Tool.CallId))
            .Reply(Call(Data.Tool.SecondCallId, "exit_plan_mode", ("plan", Data.Tool.Answer)));

        var asked = await Asking.RunAsync(Data.Tool.Question, settings: new() { Mode = AgentMode.Plan }, ct: Ct);
        var question = asked.Decisions.OfType<QuestionDecision>().Single();

        // Act
        var planned = await AnswerQuestionsAsync(question.Answer(Label.Default));

        // Assert
        planned.Decisions.OfType<PlanDecision>().Single().Plan.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_Auto_Mode_A_Question_Still_Asks_Your_App()
    {
        // Arrange
        ChatClient.Reply(Ask(Data.Tool.CallId));

        // Act
        var response = await Asking.RunAsync(Data.Tool.Question, settings: new() { Mode = AgentMode.Auto }, ct: Ct);

        // Assert
        response.Decisions.OfType<QuestionDecision>().Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Question_Next_To_A_Call_That_Needs_Approval_Both_Wait_And_Settle_Together()
    {
        // Arrange
        ChatClient
            .Reply(Calls(Ask(Data.Tool.CallId), CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket)))
            .Reply(Text(Data.Tool.Answer));

        var asked = await Asking.RunAsync(Data.Tool.Question, ct: Ct);
        var question = asked.Decisions.OfType<QuestionDecision>().Single();
        var approval = asked.Decisions.OfType<ApprovalDecision>().Single();

        // Act
        var waiting = await AnswerQuestionsAsync(question.Answer(Label.Default));
        var response = await AnswerQuestionsAsync(approval.Approve());

        // Assert
        waiting.Decisions.Select(decision => decision.Id).Should().Equal(approval.Id);
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
        ResultsSentToModel(1).Should().HaveCount(2);
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Typed_Run_That_Asks_It_Has_No_Result_And_Its_Answer_Continues_In_The_Format()
    {
        // Arrange
        var lives = JsonSerializer.Serialize(new { data = Data.Tool.Lives }, Json.SerializerOptions);

        ChatClient
            .Reply(Ask(Data.Tool.CallId))
            .Reply(Text(lives));

        var asked = await Asking.RunAsync<int>(Data.Tool.Question, ct: Ct);
        var answer = DecisionAnswer.Answer(asked.Decisions[0].Id, Label.Default);

        // Act
        var response = await Asking.AnswerAsync<int>([answer], ct: Ct);

        // Assert
        asked.TryGetResult(out _).Should().BeFalse();
        ChatClient.Options[1]!.ResponseFormat.Should().BeOfType<ChatResponseFormatJson>();
        response.Result.Should().Be(Data.Tool.Lives);
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Its_Last_Update_Carries_The_Questions()
    {
        // Arrange
        ChatClient.Reply(Ask(Data.Tool.CallId));

        // Act
        var updates = await Asking.RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct);

        // Assert
        updates.SkipLast(1).SelectMany(update => update.Decisions).Should().BeEmpty();
        updates[^1].Decisions.OfType<QuestionDecision>().Single().Questions[0].Header.Should().Be(Header);
    }

    [Fact]
    public async Task Given_A_Streamed_Answer_The_Continuation_Starts_With_The_Call_And_Its_Answers()
    {
        // Arrange
        var asked = await AskAsync();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var updates = await Asking.AnswerStreamingAsync([asked.Answer(Label.Default)], ct: Ct).ToListAsync(Ct);
        var contents = updates.SelectMany(update => update.Contents).ToList();

        // Assert
        contents.OfType<FunctionCallContent>().Select(call => call.Name).Should().Equal("ask_user");
        contents.OfType<FunctionResultContent>().Select(result => result.Result).Should().Equal(Selected(Label));
        contents.Take(2).Should().NotContain(content => content is TextContent);
        updates.ToChatResponse().Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Watcher_It_Receives_The_Questions_And_Then_The_Continuations_Updates()
    {
        // Arrange
        ChatClient
            .Reply(Ask(Data.Tool.CallId))
            .Reply(Text(Data.Tool.Answer));

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var watched = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).Take(5).ToListAsync(Ct);
        var asked = await Asking.RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct);

        // Act
        await Asking.AnswerStreamingAsync([asked[^1].Decisions[0].Reject()], ct: Ct).ToListAsync(Ct);
        var updates = await watched;

        // Assert
        updates[1].Decisions.Select(decision => decision.Id).Should().Equal(asked[^1].Decisions[0].Id);
        updates.Skip(2).SelectMany(update => update.Contents).Should().Contain(content => content is TextContent);
    }

    [Fact]
    public async Task Given_A_Handoff_To_An_Agent_That_Declares_Questions_It_Asks_And_Receives_The_Answers()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_asking_agent"))
            .Reply(Ask(Data.Tool.SecondCallId))
            .Reply(Text(Data.Tool.Answer));

        var front = Host<AskingFrontAgent>().WithConversationId(Data.Agent.ConversationId);
        var asked = await front.RunAsync(Data.Tool.Question, ct: Ct);
        var question = asked.Decisions.OfType<QuestionDecision>().Single();

        // Act
        var response = await front.AnswerAsync([question.Answer(Label.Default)], ct: Ct);

        // Assert
        OfferedTo(ChatClient, 0).Should().NotContain("ask_user");
        question.Agent.Should().Be("asking_agent");
        ChatClient.Options[2]!.ModelId.Should().Be("asking-model");
        ResultsSentToModel(2)[^1].Should().Be(Selected(Label));
        response.Text.Should().Be(Data.Tool.Answer);
    }

    private string Header => Data.Questions.Header;

    private TestValue<string> Label => Data.Questions.Label;

    // The result of the test's question answered with one of its options
    private string Selected(string label) => $$"""{"answers":[{"header":"{{Header}}","selected":["{{label}}"]}]}""";

    // A call of ask_user with arguments as a model that broke the schema sent them
    private ChatResponseUpdate Malformed(string callId, string arguments) =>
        new(
            ChatRole.Assistant,
            [
                new FunctionCallContent(
                    callId,
                    "ask_user",
                    JsonElement.Parse(arguments)
                        .EnumerateObject()
                        .ToDictionary(member => member.Name, object? (member) => member.Value))
            ]);

    private static IEnumerable<string> OfferedTo(ScriptedChatClient client, int request) =>
        client.Options[request]!.Tools?.Select(tool => tool.Name) ?? [];

    private static IEnumerable<string> ResultsSentTo(ScriptedChatClient client, int request) =>
        client.Requests[request]
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Select(result => result.Result?.ToString() ?? "");
}
