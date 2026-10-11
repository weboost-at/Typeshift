using Agentic.Conversations;
using Agentic.Tests.Conversations.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Agentic.Tests.Conversations;

public class RealtimeTrackedConversationSessionTests : ConversationTestBase
{
    [Fact]
    public async Task Given_A_Notable_Update_It_Is_Published_Before_The_Consumer_Sees_It()
    {
        // Arrange
        ChatClient
            .Reply([.. CallUpdates()])
            .Reply([.. AnswerUpdates()]);

        var stream = Tracked().GetStreamingResponseAsync(Conversation.Question, Options(), Ct);

        // Act
        var received = await ReceiveAsync(stream);
        var call = received.Single(update => update.Update.Contents is [FunctionCallContent]);

        // Assert
        call.Published[^1].Should().BeSameAs(call.Update);
    }

    [Fact]
    public async Task Given_Text_Within_The_Interval_It_Is_Not_Published_Yet()
    {
        // Arrange
        ChatClient.Reply([.. AnswerUpdates()]);

        var stream = Tracked().GetStreamingResponseAsync(Conversation.Question, Options(), Ct);

        // Act
        var received = await ReceiveAsync(stream);

        // Assert
        received.Should().OnlyContain(update => update.Published.Count == 0);
    }

    [Fact]
    public async Task Given_Text_After_The_Interval_The_Next_Delta_Is_Published_Before_The_Consumer_Sees_It()
    {
        // Arrange
        ChatClient.Reply([.. AnswerUpdates()]);

        var stream = Tracked().GetStreamingResponseAsync(Conversation.Question, Options(), Ct);

        // Act
        var received = await ReceiveAsync(stream, pause: RealtimeTrackedConversationSession.PublishInterval);

        // Assert
        received[1].Published.Should().Equal(received[0].Update, received[1].Update);
    }
}
