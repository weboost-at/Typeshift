using Typeshift.Tests.Approval.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Approval;

public class ToolApprovalTests : ApprovalTestBase
{
    [Fact]
    public async Task Given_A_Call_That_Needs_Approval_The_Run_Ends_With_Its_Request_And_It_Does_Not_Run()
    {
        // Arrange
        ChatClient.Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket));

        // Act
        var response = await RunAsync(Data.Tool.Question);

        // Assert
        response.ApprovalRequests.Select(request => ((FunctionCallContent)request.ToolCall).Name)
            .Should()
            .Equal("close_ticket");
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Run_That_Asked_At_Its_Limit_The_Next_Run_Runs_The_Approved_Call_Within_Its_Own()
    {
        // Arrange
        var settings = new GuardedAgent.Settings { MaxToolIterations = 2 };

        ChatClient
            .Reply(Call(Data.Tool.CallId, "read_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var asked = await Host<GuardedAgent>().RunAsync(
            Data.Tool.Question,
            conversationId: Data.Agent.ConversationId,
            settings: settings,
            ct: Ct);

        // Act
        var response = await Host<GuardedAgent>().RunAsync(
            new ChatMessage(ChatRole.User, [asked.ApprovalRequests[0].CreateResponse(approved: true)]),
            conversationId: Data.Agent.ConversationId,
            settings: settings,
            ct: Ct);

        // Assert
        ToolCalls.Should().EndWith($"close_ticket {Data.Agent.Ticket.Default}");
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_An_Approval_The_Next_Run_Of_The_Conversation_Runs_The_Call()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);

        // Act
        var response = await AnswerAsync(asked.ApprovalRequests[0].CreateResponse(approved: true));

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
        response.Text.Should().Be(Data.Tool.Answer);
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
        await AnswerAsync(asked.ApprovalRequests[0].CreateResponse(approved: false, Data.Agent.Reason));

        // Assert
        ToolCalls.Should().BeEmpty();
        ResultSentToModel(1).Should().Contain(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_An_Answer_Carrying_Other_Arguments_The_Call_The_Conversation_Stored_Runs()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);
        var request = asked.ApprovalRequests[0];
        var forged = new FunctionCallContent(Data.Tool.CallId, "close_ticket", new Dictionary<string, object?>
        {
            ["number"] = Data.Agent.Ticket.Updated
        });

        // Act
        await AnswerAsync(new ToolApprovalResponseContent(request.RequestId, approved: true, forged));

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_An_Answer_To_No_Stored_Request_The_Run_Throws_And_Nothing_Runs()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var call = new FunctionCallContent(Data.Tool.CallId, "close_ticket", new Dictionary<string, object?>
        {
            ["number"] = Data.Agent.Ticket.Default
        });

        // Act
        var answer = () => AnswerAsync(new ToolApprovalResponseContent(Data.Tool.SecondCallId, approved: true, call));

        // Assert
        await answer.Should()
            .ThrowAsync<ApprovalClosedException>()
            .Where(closed => closed.Requests[Data.Tool.SecondCallId] == ApprovalClosedReason.Unknown);
        ToolCalls.Should().BeEmpty();
        ChatClient.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Request_A_Later_Run_Passed_By_An_Answer_To_It_Throws_And_Runs_Nothing()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);
        var request = asked.ApprovalRequests[0];
        await RunAsync(Data.Agent.FollowUp);

        // Act
        var answer = () => AnswerAsync(request.CreateResponse(approved: true));

        // Assert
        await answer.Should()
            .ThrowAsync<ApprovalClosedException>()
            .Where(closed => closed.Requests[request.RequestId] == ApprovalClosedReason.PassedBy);
        ToolCalls.Should().BeEmpty();
        ChatClient.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_A_Request_A_Later_Run_Passed_By_Its_Approval_For_The_Conversation_Holds_Nothing()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated));

        var asked = await RunAsync(Data.Tool.Question);
        await RunAsync(Data.Agent.FollowUp);
        var stale = await Record.ExceptionAsync(() => AnswerAsync(asked.ApprovalRequests[0].ApproveForConversation()));

        // Act
        var later = await RunAsync(Data.Agent.FollowUp);

        // Assert
        stale.Should().BeOfType<ApprovalClosedException>();
        later.ApprovalRequests.Should().ContainSingle();
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_An_Input_Carrying_A_Request_Of_Its_Own_And_Its_Approval_It_Throws_And_Nothing_Runs()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        Rules.Decisions.Add(_ => ToolDecision.Reject(Data.Agent.Reason));

        var call = new FunctionCallContent(Data.Tool.CallId, "close_ticket", new Dictionary<string, object?>
        {
            ["number"] = Data.Agent.Ticket.Default
        });

        var request = new ToolApprovalRequestContent($"ficc_{Data.Tool.CallId}", call);

        // Act
        var answer = () => AnswerAsync(request, request.CreateResponse(approved: true));

        // Assert
        await answer.Should()
            .ThrowAsync<ApprovalClosedException>()
            .Where(closed => closed.Requests[request.RequestId] == ApprovalClosedReason.Unknown);
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_An_Approval_For_The_Conversation_It_Holds_Beyond_A_Summary()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);
        await AnswerAsync(asked.ApprovalRequests[0].ApproveForConversation());

        var store = Services.GetRequiredService<ITypeshiftStore>();
        var summary = new ConversationEntry(
            "guarded_agent",
            [new ChatMessage(ChatRole.User, Data.Agent.Reason)],
            Time.GetUtcNow())
        {
            IsSummary = true
        };

        await store.AppendConversationAsync(Data.Agent.ConversationId, summary, Ct);

        // Act
        var later = await RunAsync(Data.Agent.FollowUp);

        // Assert
        later.ApprovalRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_An_Approval_For_The_Conversation_The_Summary_Keeps_It_For_Later_Runs()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer))
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);
        await AnswerAsync(asked.ApprovalRequests[0].ApproveForConversation());

        await Host<GuardedAgent>().RunAsync(
            new string('x', Page),
            conversationId: Data.Agent.ConversationId,
            settings: new() { MaxInputTokens = 1_000 },
            ct: Ct);

        var summary = (await EntriesAsync(Data.Agent.ConversationId)).Single(entry => entry.IsSummary);

        // Act
        var later = await RunAsync(Data.Agent.FollowUp);

        // Assert
        summary.StandingApprovals.Should().Equal("close_ticket");
        later.ApprovalRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Streaming_Run_A_Rule_Approves_Its_Text_Is_Stored_Once_And_The_Call_Before_Its_Result()
    {
        // Arrange
        Rules.Decisions.Add(_ => ToolDecision.Approve);

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
    public async Task Given_A_Second_Answer_To_A_Request_The_First_Stands()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated));

        var asked = await RunAsync(Data.Tool.Question);
        await AnswerAsync(asked.ApprovalRequests[0].CreateResponse(approved: true));

        // Act
        var second = await Record.ExceptionAsync(() => AnswerAsync(asked.ApprovalRequests[0].ApproveForConversation()));
        var later = await RunAsync(Data.Agent.FollowUp);

        // Assert
        second.Should().BeOfType<ApprovalClosedException>();
        later.ApprovalRequests.Should().ContainSingle();
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_A_Call_Needing_Approval_Next_To_One_That_Does_Not_Only_The_First_Waits_And_Both_Run()
    {
        // Arrange
        ChatClient
            .Reply(Calls(
                CloseTicket(Data.Tool.CallId, Data.Agent.Ticket),
                Call(Data.Tool.SecondCallId, "note", ("text", Data.Tool.Query))))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);

        // Act
        await AnswerAsync(asked.ApprovalRequests.Single().CreateResponse(approved: true));

        // Assert
        ToolCalls.Should().BeEquivalentTo($"close_ticket {Data.Agent.Ticket.Default}", $"note {Data.Tool.Query}");
    }

    [Fact]
    public async Task Given_No_Conversation_A_Call_Needing_Approval_Fails_Naming_The_Tool()
    {
        // Arrange
        ChatClient.Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket));

        // Act
        var run = () => Host<GuardedAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*close_ticket*");
    }

    [Fact]
    public async Task Given_An_Approval_For_The_Conversation_A_Later_Call_Of_The_Tool_Runs_Without_Asking()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);
        await AnswerAsync(asked.ApprovalRequests[0].ApproveForConversation());

        // Act
        var later = await RunAsync(Data.Agent.FollowUp);

        // Assert
        later.ApprovalRequests.Should().BeEmpty();
        ToolCalls.Should().Equal(
            $"close_ticket {Data.Agent.Ticket.Default}",
            $"close_ticket {Data.Agent.Ticket.Updated}");
    }

    [Fact]
    public async Task Given_A_Rule_That_Approves_The_Call_Runs_Without_Asking()
    {
        // Arrange
        Rules.Decisions.Add(_ => ToolDecision.Approve);

        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await RunAsync(Data.Tool.Question);

        // Assert
        response.ApprovalRequests.Should().BeEmpty();
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_A_Rule_That_Rejects_The_Model_Receives_Its_Reason()
    {
        // Arrange
        Rules.Decisions.Add(_ => ToolDecision.Reject(Data.Agent.Reason));

        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        ToolCalls.Should().BeEmpty();
        ResultSentToModel(1).Should().Contain(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_One_Rule_That_Approves_And_One_That_Rejects_The_Rejection_Wins()
    {
        // Arrange
        Rules.Decisions.Add(_ => ToolDecision.Approve);
        Rules.Decisions.Add(_ => ToolDecision.Reject(Data.Agent.Reason));

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
    public async Task Given_An_Answered_Approval_Later_Runs_Send_The_Call_Once_With_Its_Result()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);
        await AnswerAsync(asked.ApprovalRequests[0].CreateResponse(approved: true));

        // Act
        await RunAsync(Data.Agent.FollowUp);
        var contents = ChatClient.Requests[2].SelectMany(message => message.Contents).ToList();

        // Assert
        contents.OfType<FunctionCallContent>().Select(call => call.CallId).Should().Equal(Data.Tool.CallId);
        contents.OfType<FunctionResultContent>().Select(result => result.CallId).Should().Equal(Data.Tool.CallId);
        contents.OfType<ToolApprovalRequestContent>().Should().BeEmpty();
        contents.OfType<ToolApprovalResponseContent>().Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Rule_Answering_One_Call_And_The_Host_Another_Both_Run_Once_The_Host_Answered()
    {
        // Arrange
        Rules.Decisions.Add(call => call.Arguments!["number"]!.ToString() == Data.Agent.Ticket.Default.ToString()
            ? ToolDecision.Approve
            : ToolDecision.Abstain);

        ChatClient
            .Reply(Calls(
                CloseTicket(Data.Tool.CallId, Data.Agent.Ticket),
                CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated)))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);

        // Act
        await AnswerAsync(asked.ApprovalRequests.Single().CreateResponse(approved: true));

        // Assert
        ToolCalls.Should().BeEquivalentTo(
            $"close_ticket {Data.Agent.Ticket.Default}",
            $"close_ticket {Data.Agent.Ticket.Updated}");
    }

    [Fact]
    public async Task Given_A_Streaming_Run_The_Host_Receives_The_Request_And_Its_Approval_Runs_The_Call()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var asked = await StreamAsync(Data.Tool.Question);

        // Act
        var response = await StreamAnswerAsync(asked.ApprovalRequests[0].CreateResponse(approved: true));

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Left_At_Its_Request_The_Approval_Still_Runs_The_Call()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        ToolApprovalRequestContent? request = null;

        await foreach (var update in Host<GuardedAgent>()
            .RunStreamingAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct))
        {
            request = update.Contents.OfType<ToolApprovalRequestContent>().FirstOrDefault();
            if (request is not null) break;
        }

        // Act
        await AnswerAsync(request!.CreateResponse(approved: true));

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
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
        await AnswerAsync(first.ApprovalRequests[0].CreateResponse(approved: true));
        var second = await RunAsync(Data.Agent.FollowUp);

        // Act
        await AnswerAsync(second.ApprovalRequests[0].CreateResponse(approved: true));

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
        await StreamAnswerAsync(first.ApprovalRequests[0].CreateResponse(approved: true));
        var second = await StreamAsync(Data.Agent.FollowUp);

        // Act
        await StreamAnswerAsync(second.ApprovalRequests[0].CreateResponse(approved: true));

        // Assert
        ToolCalls.Should().Equal(
            $"close_ticket {Data.Agent.Ticket.Default}",
            $"close_ticket {Data.Agent.Ticket.Updated}");
    }

    [Fact]
    public async Task Given_An_Unanswered_Request_Whose_Id_A_Later_Call_Reuses_That_Calls_Approval_Runs_It()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket.Updated))
            .Reply(Text(Data.Tool.Answer));

        await RunAsync(Data.Tool.Question);
        var second = await RunAsync(Data.Agent.FollowUp);

        // Act
        await AnswerAsync(second.ApprovalRequests[0].CreateResponse(approved: true));

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Updated}");
    }

    [Fact]
    public async Task Given_An_Unanswered_Streamed_Request_Whose_Id_A_Later_Call_Reuses_That_Calls_Approval_Runs_It()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket.Updated))
            .Reply(Text(Data.Tool.Answer));

        await StreamAsync(Data.Tool.Question);
        var second = await StreamAsync(Data.Agent.FollowUp);

        // Act
        await StreamAnswerAsync(second.ApprovalRequests[0].CreateResponse(approved: true));

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Updated}");
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

        var host = Host<GuardedAgent>();
        await host.RunAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, settings: settings, ct: Ct);
        await host.RunAsync(Data.Agent.FollowUp, conversationId: Data.Agent.ConversationId, settings: settings, ct: Ct);

        // Act
        await host.RunAsync(Data.Agent.Steering, conversationId: Data.Agent.ConversationId, settings: settings, ct: Ct);

        // Assert
        var sent = ChatClient.Requests[^1].SelectMany(message => message.Contents).ToList();
        var results = sent.OfType<FunctionResultContent>().Select(result => result.CallId);
        sent.OfType<FunctionCallContent>().Select(call => call.CallId).Should().BeEquivalentTo(results);
    }

    [Fact]
    public async Task Given_A_Streaming_Run_And_A_Rule_That_Approves_No_Request_Reaches_The_Host()
    {
        // Arrange
        Rules.Decisions.Add(_ => ToolDecision.Approve);

        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await StreamAsync(Data.Tool.Question);

        // Assert
        response.ApprovalRequests.Should().BeEmpty();
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_Answers_To_Closed_Requests_The_Exception_Names_Each_With_Why_It_Is_Closed()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated))
            .Reply(Text(Data.Tool.Answer));

        var answered = (await RunAsync(Data.Tool.Question)).ApprovalRequests[0];
        await AnswerAsync(answered.CreateResponse(approved: true));
        var passed = (await RunAsync(Data.Agent.FollowUp)).ApprovalRequests[0];
        await RunAsync(Data.Agent.FollowUp);
        var unknown = new ToolApprovalRequestContent(Data.Tool.CallId, passed.ToolCall);

        // Act
        var closed = await Record.ExceptionAsync(() => AnswerAsync(
            answered.CreateResponse(approved: true),
            passed.CreateResponse(approved: true),
            unknown.CreateResponse(approved: true))) as ApprovalClosedException;

        // Assert
        closed.Should().NotBeNull();
        closed!.Requests.Should().BeEquivalentTo(new Dictionary<string, ApprovalClosedReason>
        {
            [answered.RequestId] = ApprovalClosedReason.Answered,
            [passed.RequestId] = ApprovalClosedReason.PassedBy,
            [unknown.RequestId] = ApprovalClosedReason.Unknown
        });
    }

    [Fact]
    public async Task Given_An_Answer_To_A_Closed_Request_Nothing_Of_The_Run_Is_Stored()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var asked = await RunAsync(Data.Tool.Question);
        await RunAsync(Data.Agent.FollowUp);
        var before = await EntriesAsync(Data.Agent.ConversationId);

        // Act
        await Record.ExceptionAsync(() => AnswerAsync(
            new TextContent(Data.Agent.Steering),
            asked.ApprovalRequests[0].CreateResponse(approved: true)));

        // Assert
        (await EntriesAsync(Data.Agent.ConversationId)).Should().HaveCount(before.Count);
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Answering_A_Closed_Request_It_Throws()
    {
        // Arrange
        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var asked = await StreamAsync(Data.Tool.Question);
        await StreamAsync(Data.Agent.FollowUp);

        // Act
        var answer = () => StreamAnswerAsync(asked.ApprovalRequests[0].CreateResponse(approved: true));

        // Assert
        await answer.Should().ThrowAsync<ApprovalClosedException>();
    }

    [Fact]
    public async Task Given_Steering_The_Model_Answers_After_A_Request_The_Run_Returns_No_Request()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        var run = RunAsync(Data.Tool.Question);
        await ChatClient.Requested;
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);

        // Act
        gate.SetResult();
        var response = await run;

        // Assert
        response.ApprovalRequests.Should().BeEmpty();
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Request_Its_Steered_Run_Passed_By_An_Answer_To_It_Throws_And_Runs_Nothing()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        var run = RunAsync(Data.Tool.Question);
        await ChatClient.Requested;
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        gate.SetResult();
        await run;
        var request = (await StoredRequestsAsync()).Single();

        // Act
        var answer = () => AnswerAsync(request.CreateResponse(approved: true));

        // Assert
        await answer.Should()
            .ThrowAsync<ApprovalClosedException>()
            .Where(closed => closed.Requests[request.RequestId] == ApprovalClosedReason.PassedBy);
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Streaming_Run_Steered_After_Its_Request_An_Answer_To_It_Throws()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        var run = StreamAsync(Data.Tool.Question);
        await ChatClient.Requested;
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        gate.SetResult();
        var streamed = await run;

        // Act
        var answer = () => AnswerAsync(streamed.ApprovalRequests[0].CreateResponse(approved: true));

        // Assert
        await answer.Should().ThrowAsync<ApprovalClosedException>();
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Steered_Run_That_Failed_Before_The_Model_Answered_Its_Request_Stays_Open()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(new InvalidOperationException(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        var run = RunAsync(Data.Tool.Question);
        await ChatClient.Requested;
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        gate.SetResult();
        await Record.ExceptionAsync(() => run);
        var request = (await StoredRequestsAsync()).Single();

        // Act
        await AnswerAsync(request.CreateResponse(approved: true));

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_Steering_The_Model_Answers_By_Asking_Again_Only_The_New_Request_Is_Returned()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated));

        var run = RunAsync(Data.Tool.Question);
        await ChatClient.Requested;
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);

        // Act
        gate.SetResult();
        var response = await run;

        // Assert
        response.ApprovalRequests.Select(request => ((FunctionCallContent)request.ToolCall).CallId)
            .Should()
            .Equal(Data.Tool.SecondCallId);
    }

    [Fact]
    public async Task Given_Steering_The_Model_Answers_By_Asking_Again_The_New_Requests_Approval_Runs_It()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(gate, CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated))
            .Reply(Text(Data.Tool.Answer));

        var run = RunAsync(Data.Tool.Question);
        await ChatClient.Requested;
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        gate.SetResult();
        var asked = await run;

        // Act
        await AnswerAsync(asked.ApprovalRequests.Single().CreateResponse(approved: true));

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Updated}");
    }

    [Fact]
    public async Task Given_A_Watcher_It_Sees_Only_The_Requests_Left_To_The_Host()
    {
        // Arrange
        ChatClient.Reply(
            CloseTicket(Data.Tool.CallId, Data.Agent.Ticket),
            Call(Data.Tool.SecondCallId, "note", ("text", Data.Agent.Reason)));

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var watch = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).GetAsyncEnumerator(Ct);
        var response = watch.MoveNextAsync();

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        (await response).Should().BeTrue();
        (await watch.MoveNextAsync()).Should().BeTrue();
        watch.Current.Contents.OfType<ToolApprovalRequestContent>()
            .Select(request => ((FunctionCallContent)request.ToolCall).Name)
            .Should()
            .Equal("close_ticket");
    }

    [Fact]
    public async Task Given_No_Conversation_An_Answer_Throws_Asking_For_The_Conversation()
    {
        // Arrange
        var call = new FunctionCallContent(Data.Tool.CallId, "close_ticket", new Dictionary<string, object?>
        {
            ["number"] = Data.Agent.Ticket.Default
        });

        var request = new ToolApprovalRequestContent($"ficc_{Data.Tool.CallId}", call);

        // Act
        var answer = () => Host<GuardedAgent>().RunAsync(
            new ChatMessage(ChatRole.User, [request.CreateResponse(approved: true)]),
            ct: Ct);

        // Assert
        await answer.Should().ThrowAsync<ApprovalClosedException>().WithMessage("*without a conversation*");
        ToolCalls.Should().BeEmpty();
    }
}
