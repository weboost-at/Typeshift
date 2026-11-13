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
    public async Task Given_A_Compaction_In_A_Run_With_Context_Its_Summary_Leaves_The_Context_Out()
    {
        // Arrange
        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        Script.Context = new AgentContext { Messages = [new ChatMessage(ChatRole.User, Data.Agent.CompanyName)] };

        await RunAsync(new string('x', Page));

        // Act
        await RunAsync(Data.Agent.FollowUp, new() { MaxInputTokens = 1_000 });

        // Assert
        var summary = (await EntriesAsync(Data.Agent.ConversationId)).Single(entry => entry.IsSummary);
        summary.Messages.Skip(1).Select(message => message.Text).Should().Equal(Data.Tool.Answer, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_Context_Among_The_Summarized_Turns_The_Summary_Model_Does_Not_Read_It()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "note", ("text", new string('x', Page))))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        Script.Context = new AgentContext { Messages = [new ChatMessage(ChatRole.User, Data.Agent.CompanyName)] };

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 1_000 });

        // Assert
        ChatClient.Requests[1][1].Text.Should().Be($"user: {Data.Tool.Question}{Environment.NewLine}");
    }

    [Fact]
    public async Task Given_Context_Among_The_Summarized_Turns_The_Runs_Later_Calls_Keep_It()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "note", ("text", Data.Tool.Query)))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Call(Data.Tool.SecondCallId, "note", ("text", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        Script.Context = new AgentContext { Messages = [new ChatMessage(ChatRole.User, Data.Agent.CompanyName)] };

        // Act
        await RunAsync(new string('x', Page), new() { MaxInputTokens = 1_000 });

        // Assert
        ChatClient.Requests[2][1].Text.Should().Be(Data.Agent.CompanyName);
        ChatClient.Requests[3][1].Text.Should().Be(Data.Agent.CompanyName);
    }

    [Fact]
    public async Task Given_A_Steered_Run_With_Context_Its_Later_Pass_Starts_From_The_Summary_With_The_New_Context()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        Script.Context = new AgentContext { Messages = [new ChatMessage(ChatRole.User, Data.Agent.Instructions)] };

        ChatClient
            .Reply(Text(new string('y', Page)))
            .Reply(Text(Data.Agent.Reason))
            .Reply(gate, Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.FollowUp));

        await RunAsync(new string('x', Page));
        var run = RunAsync(Data.Tool.Question, new() { MaxInputTokens = 1_000, TargetInputTokens = 900 });
        await ChatClient.RequestedAsync(3);
        Script.Context = new AgentContext { Messages = [new ChatMessage(ChatRole.User, Data.Agent.CompanyName)] };

        // Act
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        gate.SetResult();
        await run;

        // Assert
        ChatClient.Requests[3].Skip(1).Select(message => message.Text).Should().Equal(
            Data.Agent.CompanyName,
            Data.Tool.Question,
            Data.Tool.Answer,
            Data.Agent.Steering);
    }

    [Fact]
    public async Task Given_Context_Among_The_Summarized_Turns_The_Kept_Turns_Fit_With_It()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "note", ("text", new string('x', Page))))
            .Reply(Call(Data.Tool.SecondCallId, "note", ("text", new string('x', Page))))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        Script.Context = new AgentContext { Messages = [new ChatMessage(ChatRole.User, new string('c', 2 * Page))] };

        // Act
        await RunAsync(Data.Tool.Question, new() { MaxInputTokens = 4_000, TargetInputTokens = 3_900 });

        // Assert
        ChatClient.Requests[3].SelectMany(message => message.Contents)
            .OfType<FunctionCallContent>()
            .Select(call => call.CallId)
            .Should()
            .Equal(Data.Tool.SecondCallId);
    }

    [Fact]
    public async Task Given_Only_Context_Ahead_Of_The_Input_Nothing_Is_Summarized()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        Script.Context = new AgentContext { Messages = [new ChatMessage(ChatRole.User, Data.Agent.CompanyName)] };

        // Act
        await RunAsync(new string('x', Page), new() { MaxInputTokens = 1_000 });

        // Assert
        ChatClient.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_Steering_And_Context_That_Grows_The_Steering_Input_Stays_After_The_Tool_Result()
    {
        // Arrange
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();
        Script.Context = new AgentContext { Messages = [new ChatMessage(ChatRole.User, Data.Agent.CompanyName)] };

        ChatClient
            .Reply(first, Call(Data.Tool.CallId, "note", ("text", Data.Tool.Query)))
            .Reply(second, Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        var run = RunAsync(Data.Tool.Question);
        await ChatClient.Requested;
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.Steering);
        first.SetResult();
        await ChatClient.RequestedAsync(2);
        Tracker.TrySteer(Data.Agent.ConversationId, Data.Agent.FollowUp);

        Script.Context = Script.Context with
        {
            Messages = [.. Script.Context.Messages, new ChatMessage(ChatRole.User, Data.Agent.Reason)]
        };

        // Act
        second.SetResult();
        await run;

        // Assert
        var last = ChatClient.Requests[2].ToList();
        var result = last.FindIndex(message => message.Contents.Any(content => content is FunctionResultContent));
        last[result + 1].Text.Should().Be(Data.Agent.Steering);
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
        response.Decisions.OfType<ApprovalDecision>().Select(decision => decision.Tool).Should().Equal("lookup");
    }
}
