using Typeshift.Tests.Conversations.Support;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Conversations;

public class TrackedChatClientStreamingTests : ConversationTestBase
{
    [Fact]
    public async Task Given_A_Consumed_Stream_It_Stores_The_Response_Of_Every_Update()
    {
        // Arrange
        ChatClient
            .Reply([.. CallUpdates()])
            .Reply([.. AnswerUpdates()]);

        var options = Options(Conversation.ConversationId);
        var stream = Tracked().GetStreamingResponseAsync(Conversation.Question, options, Ct);

        // Act
        await stream.ToChatResponseAsync(Ct);
        var stored = await StoredAsync(Conversation.ConversationId);

        // Assert
        stored.Sessions[0].Messages.Assert.Shaped(
            (ChatRole.User, typeof(TextContent)),
            (ChatRole.Assistant, typeof(FunctionCallContent)),
            (ChatRole.Tool, typeof(FunctionResultContent)),
            (ChatRole.Assistant, typeof(TextContent)));
    }

    [Fact]
    public async Task Given_A_Consumer_That_Stops_Early_It_Stores_What_Arrived()
    {
        // Arrange
        ChatClient.Reply([.. AnswerUpdates()]);

        var options = Options(Conversation.ConversationId);
        var stream = Tracked().GetStreamingResponseAsync(Conversation.Question, options, Ct);

        // Act
        await stream.Take(1).ToListAsync(Ct);
        var stored = await StoredAsync(Conversation.ConversationId);

        // Assert
        stored.Sessions[0].Messages.Assert.Texts(Conversation.Question, Conversation.AnswerDeltas[0]);
    }

    [Fact]
    public async Task Given_A_Cancelled_Stream_It_Stores_What_Arrived()
    {
        // Arrange
        var updates = AnswerUpdates();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        ChatClient.Reply([updates[0], new TaskCompletionSource(), .. updates.Skip(1)]);

        var options = Options(Conversation.ConversationId);
        var stream = Tracked().GetStreamingResponseAsync(Conversation.Question, options, cancellation.Token);

        // Act
        await Record.ExceptionAsync(async () =>
        {
            await foreach (var _ in stream) await cancellation.CancelAsync();
        });

        var stored = await StoredAsync(Conversation.ConversationId);

        // Assert
        stored.Sessions[0].Messages.Assert.Texts(Conversation.Question, Conversation.AnswerDeltas[0]);
    }
}
