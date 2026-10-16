using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Hosting;

public class ChatResponsesTests : UnitTestBase
{
    [Fact]
    public void Given_Two_Responses_Merging_Adds_Their_Usage_Without_Changing_The_First()
    {
        // Arrange
        var first = new ChatResponse(new ChatMessage(ChatRole.Assistant, Data.Tool.Answer))
        {
            Usage = new UsageDetails { InputTokenCount = Data.Agent.InputTokens }
        };

        var next = new ChatResponse(new ChatMessage(ChatRole.Assistant, Data.Agent.FollowUp))
        {
            Usage = new UsageDetails { InputTokenCount = Data.Agent.InputTokens }
        };

        // Act
        var merged = ChatResponses.Merge(first, next);

        // Assert
        merged.Usage!.InputTokenCount.Should().Be(Data.Agent.InputTokens * 2);
        first.Usage.InputTokenCount.Should().Be(Data.Agent.InputTokens);
    }

    [Fact]
    public void Given_Responses_Without_Usage_Merging_Leaves_The_Usage_Null()
    {
        // Arrange
        var first = new ChatResponse(new ChatMessage(ChatRole.Assistant, Data.Tool.Answer));
        var next = new ChatResponse(new ChatMessage(ChatRole.Assistant, Data.Agent.FollowUp));

        // Act
        var merged = ChatResponses.Merge(first, next);

        // Assert
        merged.Usage.Should().BeNull();
    }
}
