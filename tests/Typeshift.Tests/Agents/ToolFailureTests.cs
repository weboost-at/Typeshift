using System.Text.Json;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Testing.Fakes;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Typeshift.Tests.Agents;

public class ToolFailureTests : AgentTestBase
{
    // What the model reads of an exception nobody wrote for it
    private const string Failed = "Error: Function failed. The details of the failure are not available to you.";

    // What the tool loop writes ahead of a message the model may read
    private const string Read = "Error: Function failed. Exception:";

    private ErrorLog Log { get; } = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddSingleton<ILoggerFactory>(Log);
    }

    [Fact]
    public async Task Given_A_Tool_That_Throws_A_ToolException_The_Model_Reads_Its_Message()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "refuse", ("hint", Data.Agent.Reason)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<FailingAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        SentResult().Should().Be($"{Read} {Data.Agent.Reason}");
    }

    [Fact]
    public async Task Given_A_Tool_That_Throws_Any_Other_Exception_The_Model_Reads_Only_That_The_Call_Failed()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "crash", ("message", Data.Agent.Reason)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<FailingAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        SentResult().Should().Be(Failed);
    }

    [Fact]
    public async Task Given_A_Streaming_Run_A_Tool_That_Throws_Any_Other_Exception_Reads_Only_As_Failed()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "crash", ("message", Data.Agent.Reason)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<FailingAgent>().RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct);

        // Assert
        SentResult().Should().Be(Failed);
    }

    [Fact]
    public async Task Given_A_Call_Without_A_Required_Argument_The_Model_Reads_Which_One()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "crash"))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<FailingAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        SentResult().Should().Be($"{Read} The tool 'crash' requires the argument 'message'.");
    }

    [Fact]
    public async Task Given_A_Tool_That_Throws_Any_Other_Exception_The_Response_Holds_Nothing_Of_Its_Message()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "crash", ("message", Data.Agent.Reason)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<FailingAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Assert.ToolResult(Data.Tool.CallId).Result.Should().Be(Failed);
    }

    [Fact]
    public async Task Given_A_Tool_That_Throws_Any_Other_Exception_The_Conversation_Stores_Only_That_The_Call_Failed()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "crash", ("message", Data.Agent.Reason)))
            .Reply(Text(Data.Tool.Answer));

        var host = Host<FailingAgent>().WithConversationId(Data.Agent.ConversationId);

        // Act
        await host.RunAsync(Data.Tool.Question, ct: Ct);
        var entries = await EntriesAsync(Data.Agent.ConversationId);

        var stored = entries
            .SelectMany(entry => entry.Messages)
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Single();

        // Assert
        JsonSerializer.Serialize(stored.Result).Should().Be(JsonSerializer.Serialize(Failed));
    }

    [Fact]
    public async Task Given_A_Tool_That_Throws_Any_Other_Exception_The_Exception_Is_Logged_As_An_Error()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "crash", ("message", Data.Agent.Reason)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<FailingAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Log.Errors.Should().ContainSingle(error => error.Exception.Message == Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Tool_That_Throws_The_Error_Is_Logged_Under_The_Category_Of_The_Tool_Loop()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "crash", ("message", Data.Agent.Reason)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<FailingAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Log.Errors.Select(error => error.Category).Should().Equal(typeof(FunctionInvokingChatClient).FullName);
    }

    [Fact]
    public async Task Given_A_Tool_That_Throws_Any_Other_Exception_The_Responses_Result_Holds_The_Exception_Itself()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "crash", ("message", Data.Agent.Reason)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<FailingAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Assert.ToolResult(Data.Tool.CallId).Exception!.Message.Should().Be(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Tool_Failing_More_Rounds_In_A_Row_Than_Allowed_Its_Own_Exception_Ends_The_Run()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "crash", ("message", Data.Agent.Reason)))
            .Reply(Call(Data.Tool.SecondCallId, "crash", ("message", Data.Agent.Reason)));

        // Act
        var run = () => Host<FailingAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_ToolException_In_More_Rounds_In_A_Row_Than_Allowed_It_Ends_The_Run()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "refuse", ("hint", Data.Agent.Reason)))
            .Reply(Call(Data.Tool.SecondCallId, "refuse", ("hint", Data.Agent.Reason)));

        // Act
        var run = () => Host<FailingAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowExactlyAsync<ToolException>().WithMessage(Data.Agent.Reason);
    }

    // The result of the run's one tool call, as the second model call received it
    private object? SentResult() =>
        ChatClient.Requests[1].SelectMany(message => message.Contents).OfType<FunctionResultContent>().Single().Result;
}
