using Agentic.Conversations;
using Agentic.Tests.Conversations.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Agentic.Tests.Conversations;

public class TrackedChatClientTests : ConversationTestBase
{
    [Fact]
    public async Task Given_A_Completed_Session_It_Stores_The_Input_And_Every_Message_The_Model_Produced()
    {
        // Arrange
        ChatClient
            .Reply([.. CallUpdates()])
            .Reply([.. AnswerUpdates()]);

        // Act
        await Tracked().GetResponseAsync(Conversation.Question, Options(Conversation.ConversationId), Ct);
        var stored = await StoredAsync(Conversation.ConversationId);

        // Assert
        stored.Sessions.Should().ContainSingle();
        stored.Sessions[0].Messages.Assert.Shaped(
            (ChatRole.User, typeof(TextContent)),
            (ChatRole.Assistant, typeof(FunctionCallContent)),
            (ChatRole.Tool, typeof(FunctionResultContent)),
            (ChatRole.Assistant, typeof(TextContent)));
    }

    [Fact]
    public async Task Given_A_Failing_Inner_Client_The_Exception_Surfaces()
    {
        // Arrange
        var failure = new InvalidOperationException(Conversation.Failure);
        ChatClient.Reply(failure);

        // Act
        var ask = () => Tracked().GetResponseAsync(Conversation.Question, Options(Conversation.ConversationId), Ct);

        // Assert
        (await ask.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task Given_A_Failing_Inner_Client_The_Session_Is_Stored_With_An_Error()
    {
        // Arrange
        ChatClient.Reply(new InvalidOperationException(Conversation.Failure));
        var ask = () => Tracked().GetResponseAsync(Conversation.Question, Options(Conversation.ConversationId), Ct);

        // Act
        await Record.ExceptionAsync(ask);
        var stored = await StoredAsync(Conversation.ConversationId);

        // Assert
        stored.Sessions.Should().ContainSingle();
        stored.Sessions[0].Messages[^1].Assert.Error().Details.Should().Be(Conversation.Failure);
    }

    [Fact]
    public async Task Given_A_Failing_Inner_Client_No_Session_Stays_Tracked()
    {
        // Arrange
        ChatClient.Reply(new InvalidOperationException(Conversation.Failure));
        var ask = () => Tracked().GetResponseAsync(Conversation.Question, Options(Conversation.ConversationId), Ct);

        // Act
        await Record.ExceptionAsync(ask);

        // Assert
        Tracker.Sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Cancelled_Request_The_Stored_Error_Says_It_Was_Cancelled()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        ChatClient.Reply([gate, .. AnswerUpdates()]);

        var options = Options(Conversation.ConversationId);

        // Act
        var asking = Tracked().GetResponseAsync(Conversation.Question, options, cancellation.Token);
        await ChatClient.Requested;
        await cancellation.CancelAsync();

        await Record.ExceptionAsync(() => asking);
        var stored = await StoredAsync(Conversation.ConversationId);

        // Assert
        stored.Sessions[0].Messages[^1].Assert.Error().Message.Should().Be(
            "The request was cancelled before the model answered.");
    }

    [Fact]
    public async Task Given_An_Id_That_Is_Not_Stored_A_New_Conversation_Starts_Under_It()
    {
        // Arrange
        ChatClient.Reply([.. AnswerUpdates()]);

        // Act
        await Tracked().GetResponseAsync(Conversation.Question, Options(Conversation.ConversationId), Ct);
        var stored = await StoredAsync(Conversation.ConversationId);

        // Assert
        stored.Sessions.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Stored_Conversation_The_Model_Receives_The_Earlier_Messages_And_The_New_Input()
    {
        // Arrange
        ChatClient
            .Reply([.. AnswerUpdates()])
            .Reply([.. AnswerUpdates()]);

        var client = Tracked();
        await client.GetResponseAsync(Conversation.Question, Options(Conversation.ConversationId), Ct);

        // Act
        await client.GetResponseAsync(Conversation.FollowUp, Options(Conversation.ConversationId), Ct);

        // Assert
        ChatClient.Requests[1].Assert.Texts(Conversation.Question, Conversation.Answer, Conversation.FollowUp);
    }

    [Fact]
    public async Task Given_A_Resumed_Conversation_Its_Usage_Is_Aggregated_Over_Its_Sessions()
    {
        // Arrange
        ChatClient
            .Reply([.. AnswerUpdates()])
            .Reply([.. AnswerUpdates()]);

        var client = Tracked();
        await client.GetResponseAsync(Conversation.Question, Options(Conversation.ConversationId), Ct);

        // Act
        await client.GetResponseAsync(Conversation.FollowUp, Options(Conversation.ConversationId), Ct);
        var stored = await StoredAsync(Conversation.ConversationId);

        // Assert
        stored.AggregatedUsage.InputTokenCount.Should().Be(2 * Conversation.InputTokens);
    }

    [Fact]
    public async Task Given_A_Stored_Call_Without_A_Result_The_Resumed_Conversation_Leaves_It_Out()
    {
        // Arrange
        ChatClient
            .Reply([.. CallUpdates()])
            .Reply([.. AnswerUpdates()]);

        var client = Tracked();
        var stopped = client.GetStreamingResponseAsync(Conversation.Question, Options(Conversation.ConversationId), Ct);
        await stopped.Take(1).ToListAsync(Ct);

        // Act
        await client.GetResponseAsync(Conversation.FollowUp, Options(Conversation.ConversationId), Ct);

        // Assert
        ChatClient.Requests[1].Assert.Texts(Conversation.Question, Conversation.FollowUp);
    }

    [Fact]
    public async Task Given_A_Conversation_Id_The_Provider_Does_Not_Receive_It()
    {
        // Arrange
        ChatClient.Reply([.. AnswerUpdates()]);

        // Act
        await Tracked().GetResponseAsync(Conversation.Question, Options(Conversation.ConversationId), Ct);

        // Assert
        ChatClient.Options[0]!.ConversationId.Should().BeNull();
    }

    [Fact]
    public async Task Given_Two_Overlapping_First_Sessions_Of_A_Conversation_Both_Are_Stored()
    {
        // Arrange
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();

        ChatClient
            .Reply([first, .. AnswerUpdates()])
            .Reply([second, .. AnswerUpdates()]);

        var client = Tracked();
        var asking = client.GetResponseAsync(Conversation.Question, Options(Conversation.ConversationId), Ct);
        var askingAgain = client.GetResponseAsync(Conversation.FollowUp, Options(Conversation.ConversationId), Ct);

        // Act
        first.SetResult();
        await asking;
        second.SetResult();
        await askingAgain;

        var stored = await StoredAsync(Conversation.ConversationId);

        // Assert
        stored.Sessions.Should().HaveCount(2);
    }
}
