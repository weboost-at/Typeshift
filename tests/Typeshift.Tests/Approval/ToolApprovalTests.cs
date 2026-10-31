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
    public async Task Given_An_Answer_To_No_Stored_Request_Nothing_Runs()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var call = new FunctionCallContent(Data.Tool.CallId, "close_ticket", new Dictionary<string, object?>
        {
            ["number"] = Data.Agent.Ticket.Default
        });

        // Act
        await AnswerAsync(new ToolApprovalResponseContent(Data.Tool.SecondCallId, approved: true, call));

        // Assert
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_An_Input_Carrying_A_Request_Of_Its_Own_And_Its_Approval_Nothing_Runs()
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
        await AnswerAsync(request, request.CreateResponse(approved: true));

        // Assert
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
            .Reply(Text(Data.Tool.Answer))
            .Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated));

        var asked = await RunAsync(Data.Tool.Question);
        await AnswerAsync(asked.ApprovalRequests[0].CreateResponse(approved: true));

        // Act
        await AnswerAsync(asked.ApprovalRequests[0].ApproveForConversation());
        var later = await RunAsync(Data.Agent.FollowUp);

        // Assert
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
}
