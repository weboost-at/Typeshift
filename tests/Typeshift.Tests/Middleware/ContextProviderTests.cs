using Typeshift.Tests.Middleware.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Middleware;

public class ContextProviderTests : MiddlewareTestBase
{
    [Fact]
    public async Task Given_A_Context_Provider_Its_Instructions_Follow_The_Agents()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        Script.Context = new AgentContext { Instructions = Data.Agent.Instructions };

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        ChatClient.Options[0]!.Instructions.Should().Be($"{ContextAgent.Prompt}\n\n{Data.Agent.Instructions}");
    }

    [Fact]
    public async Task Given_A_Context_Provider_Its_Messages_Go_Ahead_Of_The_Input_And_Are_Not_Stored()
    {
        // Arrange
        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        Script.Context = new AgentContext { Messages = [new ChatMessage(ChatRole.User, Data.Agent.CompanyName)] };

        await RunAsync(Data.Tool.Question);

        // Act
        await RunAsync(Data.Agent.FollowUp);

        // Assert
        ChatClient.Requests[1].Select(message => message.Text).Should().Equal(
            Data.Tool.Question,
            Data.Tool.Answer,
            Data.Agent.CompanyName,
            Data.Agent.FollowUp);
        (await EntriesAsync(Data.Agent.ConversationId))
            .SelectMany(entry => entry.Messages)
            .Select(message => message.Text)
            .Should()
            .NotContain(Data.Agent.CompanyName);
    }

    [Fact]
    public async Task Given_A_Context_Provider_Its_Tools_Are_Offered_After_The_Agents()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        Script.Context = new AgentContext { Tools = [AIFunctionFactory.Create(() => Data.Tool.Answer, "lookup")] };

        // Act
        await RunAsync(Data.Tool.Question);

        // Assert
        ChatClient.Options[0]!.Tools!.Select(tool => tool.Name)
            .Should()
            .Equal("close_ticket", "read_ticket", "note", "lookup");
    }

    [Fact]
    public async Task Given_A_Context_Tool_That_Requires_Approval_The_Run_Asks_For_It()
    {
        // Arrange
        ChatClient.Reply(Call(Data.Tool.CallId, "lookup"));

        var lookup = new ApprovalRequiredAIFunction(AIFunctionFactory.Create(() => Data.Tool.Answer, "lookup"));
        Script.Context = new AgentContext { Tools = [lookup] };

        // Act
        var response = await RunAsync(Data.Tool.Question);

        // Assert
        response.ApprovalRequests.Select(request => ((FunctionCallContent)request.ToolCall).Name)
            .Should()
            .Equal("lookup");
    }
}
