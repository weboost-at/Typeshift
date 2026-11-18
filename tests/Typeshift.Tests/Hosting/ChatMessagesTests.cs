using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Hosting;

public class ChatMessagesTests : UnitTestBase
{
    [Fact]
    public void Given_Every_Content_Kept_The_Message_Itself_Is_Returned()
    {
        // Arrange
        var message = new ChatMessage(ChatRole.Assistant, [new TextContent(Data.Tool.Answer)]);

        // Act
        var kept = message.Keeping(_ => true);

        // Assert
        kept.Should().BeSameAs(message);
    }

    [Fact]
    public void Given_No_Content_Kept_No_Message_Is_Returned()
    {
        // Arrange
        var message = new ChatMessage(ChatRole.Assistant, [new TextContent(Data.Tool.Answer)]);

        // Act
        var kept = message.Keeping(_ => false);

        // Assert
        kept.Should().BeNull();
    }

    [Fact]
    public void Given_Some_Contents_Kept_A_Copy_Holds_Them_And_The_Message_Keeps_Its_Own()
    {
        // Arrange
        var text = new TextContent(Data.Tool.Answer);
        var call = new FunctionCallContent(Data.Tool.CallId, "probe");
        var message = new ChatMessage(ChatRole.Assistant, [text, call]);

        // Act
        var kept = message.Keeping(content => content is FunctionCallContent);

        // Assert
        kept!.Contents.Should().Equal(call);
        message.Contents.Should().Equal(text, call);
    }
}
