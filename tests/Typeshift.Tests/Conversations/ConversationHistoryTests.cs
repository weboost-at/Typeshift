using Typeshift.Tests.Conversations.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Conversations;

public class ConversationHistoryTests : UnitTestBase
{
    [Fact]
    public void Given_A_Result_Stored_After_A_Later_Message_It_Follows_The_Message_That_Made_Its_Call()
    {
        // Arrange
        ConversationEntry[] entries =
        [
            Steps.Entry(Steps.Input(Data.Tool.Question)),
            Steps.Entry(Steps.Calling(Data.Tool.CallId)),
            Steps.Entry(Steps.Input(Data.Agent.Steering)),
            Steps.Entry(Steps.Results(Data.Tool.CallId)),
            Steps.Entry(Steps.Answer(Data.Tool.Answer))
        ];

        // Act
        var history = ConversationHistory.From(entries);

        // Assert
        history.Select(message => message.Role)
            .Should()
            .Equal(ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.User, ChatRole.Assistant);
        history[3].Text.Should().Be(Data.Agent.Steering);
    }

    [Fact]
    public void Given_Results_Of_One_Response_In_Two_Entries_Both_Follow_It()
    {
        // Arrange
        ConversationEntry[] entries =
        [
            Steps.Entry(Steps.Calling(Data.Tool.CallId, Data.Tool.SecondCallId)),
            Steps.Entry(Steps.Results(Data.Tool.SecondCallId)),
            Steps.Entry(Steps.Input(Data.Agent.Steering)),
            Steps.Entry(Steps.Results(Data.Tool.CallId))
        ];

        // Act
        var history = ConversationHistory.From(entries);

        // Assert
        history.Select(message => message.Role)
            .Should()
            .Equal(ChatRole.Assistant, ChatRole.Tool, ChatRole.Tool, ChatRole.User);
        ResultIds(history).Should().BeEquivalentTo(Data.Tool.CallId, Data.Tool.SecondCallId);
    }

    [Fact]
    public void Given_A_Result_Whose_Call_Is_Missing_It_Is_Left_Out()
    {
        // Arrange
        ConversationEntry[] entries =
        [
            Steps.Entry(Steps.Input(Data.Tool.Question)),
            Steps.Entry(Steps.Results(Data.Tool.CallId)),
            Steps.Entry(Steps.Answer(Data.Tool.Answer))
        ];

        // Act
        var history = ConversationHistory.From(entries);

        // Assert
        history.Select(message => message.Role).Should().Equal(ChatRole.User, ChatRole.Assistant);
    }

    [Fact]
    public void Given_A_Call_Without_A_Result_It_Is_Left_Out()
    {
        // Arrange
        ConversationEntry[] entries =
        [
            Steps.Entry(Steps.Input(Data.Tool.Question)),
            Steps.Entry(Steps.Calling(Data.Tool.CallId, Data.Tool.SecondCallId)),
            Steps.Entry(Steps.Results(Data.Tool.SecondCallId))
        ];

        // Act
        var history = ConversationHistory.From(entries);

        // Assert
        CallIds(history).Should().Equal(Data.Tool.SecondCallId);
        ResultIds(history).Should().Equal(Data.Tool.SecondCallId);
    }

    [Fact]
    public void Given_A_Call_And_Its_Result_Stored_Twice_The_Last_Copy_Of_Each_Stays()
    {
        // Arrange
        ConversationEntry[] entries =
        [
            Steps.Entry(Steps.Calling(Data.Tool.CallId)),
            Steps.Entry(Steps.Calling(Data.Tool.CallId)),
            Steps.Entry(Steps.Results(Data.Tool.CallId)),
            Steps.Entry(Steps.Results(Data.Tool.CallId))
        ];

        // Act
        var history = ConversationHistory.From(entries);

        // Assert
        history.Select(message => message.Role).Should().Equal(ChatRole.Assistant, ChatRole.Tool);
        history[0].Should().BeSameAs(entries[1].Messages[0]);
        history[1].Should().BeSameAs(entries[3].Messages[0]);
    }

    [Fact]
    public void Given_A_Result_In_The_Message_That_Made_Its_Call_It_Stays_There()
    {
        // Arrange
        var served = new ChatMessage(
            ChatRole.Assistant,
            [new FunctionCallContent(Data.Tool.CallId, "probe"), new FunctionResultContent(Data.Tool.CallId, "done")]);

        // Act
        var history = ConversationHistory.From([Steps.Entry(served)]);

        // Assert
        history.Should().Equal(served);
    }

    [Fact]
    public void Given_Approval_Contents_An_Earlier_Version_Stored_They_Are_Left_Out()
    {
        // Arrange
        var request = new ToolApprovalRequestContent(
            Data.Decisions.DecisionId,
            new FunctionCallContent(Data.Tool.CallId, "probe"));

        ConversationEntry[] entries =
        [
            Steps.Entry(Steps.Input(Data.Tool.Question)),
            Steps.Entry(Steps.Calling(Data.Tool.CallId)),
            Steps.Entry(new ChatMessage(ChatRole.Assistant, [request])),
            Steps.Entry(new ChatMessage(ChatRole.User, [request.CreateResponse(approved: true)])),
            Steps.Entry(Steps.Results(Data.Tool.CallId)),
            Steps.Entry(Steps.Answer(Data.Tool.Answer))
        ];

        // Act
        var history = ConversationHistory.From(entries);

        // Assert
        history.Select(message => message.Role)
            .Should()
            .Equal(ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.Assistant);
    }

    private static IEnumerable<string> CallIds(IEnumerable<ChatMessage> messages) =>
        messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().Select(call => call.CallId);

    private static IEnumerable<string> ResultIds(IEnumerable<ChatMessage> messages) =>
        messages.SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Select(result => result.CallId);
}
