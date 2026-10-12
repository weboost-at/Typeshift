using Typeshift.Conversations;
using Typeshift.Tests.Conversations.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Conversations;

public class SubscriberTests : ConversationTestBase
{
    [Fact]
    public async Task Given_Throwing_Subscribers_The_Stream_Completes()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        ChatClient.Reply([gate, .. AnswerUpdates()]);
        Tracker.OnConversationsChanged += Throw;

        // Act
        var streaming = Tracked()
            .GetStreamingResponseAsync(Conversation.Question, Options(), Ct)
            .ToChatResponseAsync(Ct);
        await ChatClient.Requested;

        var session = Tracker.RealtimeSessions[0];
        session.OnSessionUpdated += Throw;
        session.OnSessionCompleted += Throw;
        gate.SetResult();

        var response = await streaming;

        // Assert
        response.Text.Should().Be(Conversation.Answer);

        Task Throw(CancellationToken ct) => throw new InvalidOperationException(Conversation.Failure);
    }

    [Fact]
    public async Task Given_A_Subscriber_That_Never_Finishes_The_Call_Goes_On_At_The_Limit()
    {
        // Arrange
        using var entered = new SemaphoreSlim(0);
        ChatClient.Reply([.. AnswerUpdates()]);
        Tracker.OnConversationsChanged += _ => Hang(entered);

        // Act
        var asking = Tracked().GetResponseAsync(Conversation.Question, Options(), Ct);

        await entered.WaitAsync(Ct);
        Time.Advance(ConversationTracker.SubscriberLimit);
        await entered.WaitAsync(Ct);
        Time.Advance(ConversationTracker.SubscriberLimit);

        var response = await asking;

        // Assert
        response.Text.Should().Be(Conversation.Answer);
    }

    [Fact]
    public async Task Given_A_Session_Subscriber_That_Never_Finishes_The_Stream_Goes_On_At_The_Limit()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        using var entered = new SemaphoreSlim(0);
        ChatClient.Reply([gate, .. AnswerUpdates()]);

        // Act
        var streaming = Tracked()
            .GetStreamingResponseAsync(Conversation.Question, Options(), Ct)
            .ToChatResponseAsync(Ct);
        await ChatClient.Requested;

        var session = Tracker.RealtimeSessions[0];
        session.OnSessionUpdated += _ => Hang(entered);
        session.OnSessionCompleted += _ => Hang(entered);
        gate.SetResult();

        await entered.WaitAsync(Ct);
        Time.Advance(RealtimeTrackedConversationSession.SubscriberLimit);
        await entered.WaitAsync(Ct);
        Time.Advance(RealtimeTrackedConversationSession.SubscriberLimit);

        var response = await streaming;

        // Assert
        response.Text.Should().Be(Conversation.Answer);
    }

    [Fact]
    public async Task Given_A_Subscriber_Its_Token_Is_Cancelled_At_The_Limit()
    {
        // Arrange
        using var entered = new SemaphoreSlim(0);
        List<CancellationToken> tokens = [];
        ChatClient.Reply([.. AnswerUpdates()]);

        Tracker.OnConversationsChanged += ct =>
        {
            tokens.Add(ct);
            return Hang(entered).WaitAsync(ct);
        };

        // Act
        var asking = Tracked().GetResponseAsync(Conversation.Question, Options(), Ct);

        await entered.WaitAsync(Ct);
        Time.Advance(ConversationTracker.SubscriberLimit);
        await entered.WaitAsync(Ct);
        Time.Advance(ConversationTracker.SubscriberLimit);

        await asking;

        // Assert
        tokens.Select(token => token.IsCancellationRequested).Should().Equal(true, true);
    }
}
