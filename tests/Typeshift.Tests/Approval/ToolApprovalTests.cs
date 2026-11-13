using System.Text.Json;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Approval.Support;
using Typeshift.Tests.Conversations.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Approval;

public class ToolApprovalTests : ApprovalTestBase
{
    [Fact]
    public async Task Given_A_Call_That_Needs_Approval_The_Run_Ends_With_Its_Decision_And_It_Does_Not_Run()
    {
        // Arrange
        ChatClient.Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket));

        // Act
        var response = await RunAsync(Data.Tool.Question);

        // Assert
        response.Decisions.OfType<ApprovalDecision>().Select(decision => decision.Tool).Should().Equal("close_ticket");
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Decision_It_Carries_The_Arguments_The_Model_Sent_And_The_Agent_That_Asked()
    {
        // Arrange
        ChatClient.Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket));

        // Act
        var decision = (ApprovalDecision)(await RunAsync(Data.Tool.Question)).Decisions.Single();

        // Assert
        decision.Arguments.GetProperty("number").GetInt32().Should().Be(Data.Agent.Ticket.Default);
        decision.Agent.Should().Be("guarded_agent");
        decision.AskedAt.Should().Be(Time.GetUtcNow());
    }

    [Fact]
    public async Task Given_A_Run_That_Asked_At_Its_Limit_The_Answering_Run_Runs_The_Approved_Call_Within_Its_Own()
    {
        // Arrange
        var settings = new GuardedAgent.Settings { MaxToolIterations = 2 };

        ChatClient
            .Reply(Call(Data.Tool.CallId, "read_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var asked = await Chat.RunAsync(Data.Tool.Question, settings: settings, ct: Ct);

        // Act
        var response = await Chat.AnswerAsync([DecisionAnswer.Approve(asked.Decisions[0].Id)], settings, Ct);

        // Assert
        ToolCalls.Should().EndWith($"close_ticket {Data.Agent.Ticket.Default}");
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_An_Approval_The_Call_Runs_With_The_Stored_Arguments_And_The_Run_Goes_On()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);

        // Act
        var response = await AnswerAsync(asked.Decisions.OfType<ApprovalDecision>().Single().Approve());

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
        response.Text.Should().Be(Data.Tool.Answer);
        response.Decisions.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_An_Approval_In_A_Typed_Run_The_Run_Goes_On_In_Its_Format()
    {
        // Arrange
        var lives = JsonSerializer.Serialize(new { data = Data.Tool.Lives }, Json.SerializerOptions);

        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(lives));

        var asked = await Chat.RunAsync<int>(Data.Tool.Question, ct: Ct);

        // Act
        var response = await Chat.AnswerAsync<int>([DecisionAnswer.Approve(asked.Decisions[0].Id)], ct: Ct);

        // Assert
        ChatClient.Options[1]!.ResponseFormat.Should().BeOfType<ChatResponseFormatJson>();
        response.Result.Should().Be(Data.Tool.Lives);
    }

    [Fact]
    public async Task Given_A_Typed_Run_That_Waits_It_Has_No_Result_Yet()
    {
        // Arrange
        ChatClient.Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket));

        // Act
        var asked = await Chat.RunAsync<int>(Data.Tool.Question, ct: Ct);

        // Assert
        asked.TryGetResult(out _).Should().BeFalse();
        asked.Decisions.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Rejection_The_Model_Receives_Its_Reason_And_The_Call_Does_Not_Run()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);

        // Act
        await AnswerAsync(asked.Decisions[0].Reject(Data.Agent.Reason));

        // Assert
        ToolCalls.Should().BeEmpty();
        ResultSentToModel(1).Should().Be($"The user rejected this call. {Data.Agent.Reason}");
    }

    [Fact]
    public async Task Given_A_Call_Needing_Approval_Next_To_One_That_Does_Not_Neither_Runs_Before_The_Answer()
    {
        // Arrange
        ChatClient.Reply(Calls(
            CloseTicket(Data.Tool.CallId, Data.Agent.Ticket),
            Note(Data.Tool.SecondCallId, Data.Tool.Query)));

        // Act
        var asked = await RunAsync(Data.Tool.Question);

        // Assert
        asked.Decisions.Should().ContainSingle();
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_An_Approval_The_Call_Next_To_It_Runs_With_It()
    {
        // Arrange
        ChatClient
            .Reply(Calls(
                CloseTicket(Data.Tool.CallId, Data.Agent.Ticket),
                Note(Data.Tool.SecondCallId, Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);

        // Act
        await AnswerAsync(DecisionAnswer.Approve(asked.Decisions.Single().Id));

        // Assert
        ToolCalls.Should().BeEquivalentTo($"close_ticket {Data.Agent.Ticket.Default}", $"note {Data.Tool.Query}");
    }

    [Fact]
    public async Task Given_A_Rejection_The_Call_Next_To_It_Still_Runs()
    {
        // Arrange
        ChatClient
            .Reply(Calls(
                CloseTicket(Data.Tool.CallId, Data.Agent.Ticket),
                Note(Data.Tool.SecondCallId, Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);

        // Act
        await AnswerAsync(asked.Decisions.Single().Reject());

        // Assert
        ToolCalls.Should().Equal($"note {Data.Tool.Query}");
    }

    [Fact]
    public async Task Given_No_Conversation_A_Call_Needing_Approval_Throws_Naming_The_Tool()
    {
        // Arrange
        ChatClient.Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket));

        // Act
        var run = () => Host<GuardedAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should()
            .ThrowAsync<DecisionUnavailableException>()
            .Where(unavailable => unavailable.Tools.SequenceEqual(new[] { "close_ticket" }));
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Rule_That_Approves_The_Call_Runs_Without_Asking_And_Nothing_Is_Written_For_It()
    {
        // Arrange
        Rules.Verdicts.Add(_ => RuleVerdict.Approve);

        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await RunAsync(Data.Tool.Question);

        // Assert
        response.Decisions.Should().BeEmpty();
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
        (await StoredDecisionsAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Given_A_Rule_That_Rejects_The_Model_Receives_Its_Reason()
    {
        // Arrange
        Rules.Verdicts.Add(_ => RuleVerdict.Reject(Data.Agent.Reason));

        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        ToolCalls.Should().BeEmpty();
        ResultSentToModel(1).Should().Be(Data.Agent.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Given_A_Rejection_Without_A_Reason_The_Rule_Fails_Naming_The_Argument(string? reason)
    {
        // Act
        var reject = () => RuleVerdict.Reject(reason!);

        // Assert
        reject.Should().Throw<ArgumentException>().WithParameterName("reason");
    }

    [Fact]
    public async Task Given_One_Rule_That_Approves_And_One_That_Rejects_The_Rejection_Wins()
    {
        // Arrange
        Rules.Verdicts.Add(_ => RuleVerdict.Approve);
        Rules.Verdicts.Add(_ => RuleVerdict.Reject(Data.Agent.Reason));

        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Call_Needing_Approval_A_Rule_Sees_It_With_The_Runs_Mode()
    {
        // Arrange
        ChatClient.Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket));

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        Rules.Seen.Should().Equal(("close_ticket", AgentMode.Default));
    }

    [Fact]
    public async Task Given_A_Rule_Deciding_One_Call_And_Your_App_Another_Both_Run_Once_The_App_Answered()
    {
        // Arrange
        Rules.Verdicts.Add(call => call.Arguments!["number"]!.ToString() == Data.Agent.Ticket.Default.ToString()
            ? RuleVerdict.Approve
            : RuleVerdict.Abstain);

        ChatClient
            .Reply(Calls(
                CloseTicket(Data.Tool.CallId, Data.Agent.Ticket),
                CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated)))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);

        // Act
        await AnswerAsync(DecisionAnswer.Approve(asked.Decisions.Single().Id));

        // Assert
        ToolCalls.Should().BeEquivalentTo(
            $"close_ticket {Data.Agent.Ticket.Default}",
            $"close_ticket {Data.Agent.Ticket.Updated}");
    }

    [Fact]
    public async Task Given_An_Answered_Approval_Later_Runs_Send_The_Call_Once_With_Its_Result()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);
        await AnswerAsync(DecisionAnswer.Approve(asked.Decisions[0].Id));

        // Act
        await RunAsync(Data.Agent.FollowUp);
        var contents = ChatClient.Requests[2].SelectMany(message => message.Contents).ToList();

        // Assert
        contents.OfType<FunctionCallContent>().Select(call => call.CallId).Should().Equal(Data.Tool.CallId);
        contents.OfType<FunctionResultContent>().Select(result => result.CallId).Should().Equal(Data.Tool.CallId);
    }

    [Fact]
    public async Task Given_An_Answered_Approval_The_Conversation_Stores_Its_Call_And_Its_Result_Once()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);

        // Act
        await AnswerAsync(DecisionAnswer.Approve(asked.Decisions[0].Id));
        var stored = (await EntriesAsync(Data.Agent.ConversationId))
            .SelectMany(entry => entry.Messages)
            .SelectMany(message => message.Contents)
            .ToList();

        // Assert
        stored.OfType<FunctionCallContent>().Should().ContainSingle();
        stored.OfType<FunctionResultContent>().Should().ContainSingle();
        stored.Should().NotContain(content => content is InputRequestContent || content is InputResponseContent);
    }

    [Fact]
    public async Task Given_Input_That_Carries_An_Approval_Answer_The_Run_Throws_Naming_AnswerAsync()
    {
        // Arrange
        var call = new FunctionCallContent(Data.Tool.CallId, "close_ticket");
        var answer = new ToolApprovalRequestContent(Data.Tool.CallId, call).CreateResponse(approved: true);

        // Act
        var run = () => Chat.RunAsync(new ChatMessage(ChatRole.User, [answer]), ct: Ct);

        // Assert
        await run.Should().ThrowAsync<ArgumentException>().WithMessage("*AnswerAsync*");
        ChatClient.Requests.Should().BeEmpty();
        (await EntriesAsync(Data.Agent.ConversationId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Its_Last_Update_Carries_The_Decision_And_Its_Approval_Runs_The_Call()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var updates = await Chat.RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct);

        // Act
        var response = await StreamAnswerAsync(DecisionAnswer.Approve(updates[^1].Decisions.Single().Id));

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Left_Before_Its_Last_Update_The_Decision_Loads_And_Its_Approval_Runs()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        await foreach (var update in Chat.RunStreamingAsync(Data.Tool.Question, ct: Ct))
        {
            if (update.Contents.Any(content => content is FunctionCallContent)) break;
        }

        var open = (await DecisionsAsync()).Open;

        // Act
        await AnswerAsync(DecisionAnswer.Approve(open.Single().Id));

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_A_Streamed_Decision_Its_Update_Keeps_The_Ids_And_Finish_Reason_Of_The_Asking_Response()
    {
        // Arrange
        var call = CloseTicket(Data.Tool.CallId, Data.Agent.Ticket);
        call.MessageId = "msg_1";
        call.ResponseId = "resp_1";
        call.FinishReason = ChatFinishReason.ToolCalls;
        ChatClient.Reply(call);

        // Act
        var updates = await Chat.RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct);
        var response = updates.ToChatResponse();

        // Assert
        updates[^1].MessageId.Should().Be("msg_1");
        updates[^1].ResponseId.Should().Be("resp_1");
        response.FinishReason.Should().Be(ChatFinishReason.ToolCalls);
        response.Decisions.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Streaming_Run_And_A_Rule_That_Approves_No_Decision_Reaches_Your_App()
    {
        // Arrange
        Rules.Verdicts.Add(_ => RuleVerdict.Approve);

        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await StreamAsync(Data.Tool.Question);

        // Assert
        response.Decisions.Should().BeEmpty();
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_A_Streaming_Run_A_Rule_Approves_Its_Text_Is_Stored_Once_And_The_Call_Before_Its_Result()
    {
        // Arrange
        Rules.Verdicts.Add(_ => RuleVerdict.Approve);

        ChatClient
            .Reply(Text(Data.Agent.FollowUp), CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await StreamAsync(Data.Tool.Question);
        var stored = (await EntriesAsync(Data.Agent.ConversationId)).SelectMany(entry => entry.Messages).ToList();

        // Assert
        stored.Count(message => message.Text == Data.Agent.FollowUp).Should().Be(1);
        stored.FindIndex(message => message.Contents.Any(content => content is FunctionResultContent))
            .Should()
            .Be(stored.FindIndex(message => message.Contents.Any(content => content is FunctionCallContent)) + 1);
    }

    [Fact]
    public async Task Given_A_Provider_That_Reuses_Call_Ids_Across_Runs_A_Later_Approval_Runs_Its_Call()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket.Updated))
            .Reply(Text(Data.Tool.Answer));

        var first = await RunAsync(Data.Tool.Question);
        await AnswerAsync(DecisionAnswer.Approve(first.Decisions[0].Id));
        var second = await RunAsync(Data.Agent.FollowUp);

        // Act
        await AnswerAsync(DecisionAnswer.Approve(second.Decisions[0].Id));

        // Assert
        ToolCalls.Should().Equal(
            $"close_ticket {Data.Agent.Ticket.Default}",
            $"close_ticket {Data.Agent.Ticket.Updated}");
    }

    [Fact]
    public async Task Given_A_Provider_That_Reuses_Call_Ids_Across_Streaming_Runs_A_Later_Approval_Runs_Its_Call()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket.Updated))
            .Reply(Text(Data.Tool.Answer));

        var first = await StreamAsync(Data.Tool.Question);
        await StreamAnswerAsync(DecisionAnswer.Approve(first.Decisions[0].Id));
        var second = await StreamAsync(Data.Agent.FollowUp);

        // Act
        await StreamAnswerAsync(DecisionAnswer.Approve(second.Decisions[0].Id));

        // Assert
        ToolCalls.Should().Equal(
            $"close_ticket {Data.Agent.Ticket.Default}",
            $"close_ticket {Data.Agent.Ticket.Updated}");
    }

    [Fact]
    public async Task Given_A_Decision_Passed_By_Whose_Call_Id_A_Later_Call_Reuses_That_Calls_Approval_Runs_It()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket.Updated))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(Data.Tool.Question);
        var second = await RunAsync(Data.Agent.FollowUp);

        // Act
        await AnswerAsync(DecisionAnswer.Approve(second.Decisions[0].Id));

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Updated}");
    }

    [Fact]
    public async Task Given_A_Summary_Between_Two_Calls_Of_One_Id_An_Answer_To_The_First_Throws_And_Runs_Nothing()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer))
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket.Updated));

        var first = await RunAsync(Data.Tool.Question);
        var stale = DecisionAnswer.Approve(first.Decisions[0].Id);
        await AnswerAsync(stale);
        await Chat.RunAsync(new string('x', Page), settings: new() { MaxInputTokens = 1_000 }, ct: Ct);
        var second = await RunAsync(Data.Agent.FollowUp);

        // Act
        var answer = () => AnswerAsync(stale);

        // Assert
        await answer.Should().ThrowAsync<DecisionClosedException>();
        second.Decisions.Should().ContainSingle();
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_A_Call_Left_At_The_Limit_Whose_Id_A_Later_Call_Reuses_Each_Call_Goes_Out_With_Its_Result()
    {
        // Arrange
        var settings = new GuardedAgent.Settings { MaxToolIterations = 1 };

        ChatClient
            .Reply(Call(Data.Tool.CallId, "read_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Call(Data.Tool.SecondCallId, "read_ticket", ("number", Data.Agent.Ticket.Updated)))
            .Reply(Call(Data.Tool.SecondCallId, "read_ticket", ("number", Data.Agent.Ticket.Updated)))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        await Chat.RunAsync(Data.Tool.Question, settings: settings, ct: Ct);
        await Chat.RunAsync(Data.Agent.FollowUp, settings: settings, ct: Ct);

        // Act
        await Chat.RunAsync(Data.Agent.Steering, settings: settings, ct: Ct);

        // Assert
        var sent = ChatClient.Requests[^1].SelectMany(message => message.Contents).ToList();
        var results = sent.OfType<FunctionResultContent>().Select(result => result.CallId);
        sent.OfType<FunctionCallContent>().Select(call => call.CallId).Should().BeEquivalentTo(results);
    }

    [Fact]
    public async Task Given_A_Store_That_Appended_Each_Entry_Twice_An_Approval_Still_Runs_Its_Call_Once()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<DoublingStore>().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models).AddSingleton(ToolCalls).AddSingleton(Rules);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var chat = scope.ServiceProvider.GetRequiredService<AgentHost<GuardedAgent>>()
            .WithConversationId(Data.Agent.ConversationId);

        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        var asked = await chat.RunAsync(Data.Tool.Question, ct: Ct);
        await chat.AnswerAsync([DecisionAnswer.Approve(asked.Decisions[0].Id)], ct: Ct);

        // Act
        await chat.RunAsync(Data.Agent.FollowUp, ct: Ct);
        var sent = ChatClient.Requests[^1].SelectMany(message => message.Contents).ToList();

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
        sent.OfType<FunctionCallContent>().Should().ContainSingle();
        sent.OfType<FunctionResultContent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Watcher_It_Receives_The_Decisions_Once_The_Run_Ended()
    {
        // Arrange
        ChatClient.Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket));

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var watch = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).GetAsyncEnumerator(Ct);
        var called = watch.MoveNextAsync();

        // Act
        var response = await RunAsync(Data.Tool.Question);

        // Assert
        (await called).Should().BeTrue();
        (await watch.MoveNextAsync()).Should().BeTrue();
        watch.Current.Decisions.Select(decision => decision.Id).Should().Equal(response.Decisions[0].Id);
    }
}
