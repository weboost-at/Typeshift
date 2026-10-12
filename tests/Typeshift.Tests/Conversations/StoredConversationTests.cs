using System.Text.Json;
using Typeshift.Conversations;
using Typeshift.Tests.Conversations.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Conversations;

public class StoredConversationTests : ConversationTestBase
{
    [Fact]
    public async Task Given_A_Stored_Conversation_It_Round_Trips_Through_Json()
    {
        // Arrange
        ChatClient
            .Reply([.. CallUpdates()])
            .Reply([.. AnswerUpdates()]);

        await Tracked().GetResponseAsync(Conversation.Question, Options(Conversation.ConversationId), Ct);
        var typeInfo = Json.GetTypeInfo<StoredConversation>();
        var json = JsonSerializer.Serialize(await StoredAsync(Conversation.ConversationId), typeInfo);

        // Act
        var restored = JsonSerializer.Deserialize(json, typeInfo)!;

        // Assert
        JsonSerializer.Serialize(restored, typeInfo).Should().Be(json);
        restored.Sessions[0].Messages.Assert.Shaped(
            (ChatRole.User, typeof(TextContent)),
            (ChatRole.Assistant, typeof(FunctionCallContent)),
            (ChatRole.Tool, typeof(FunctionResultContent)),
            (ChatRole.Assistant, typeof(TextContent)));
    }
}
