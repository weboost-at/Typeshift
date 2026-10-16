using System.Diagnostics;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Conversations.Support;
using Typeshift.Tests.Telemetry.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Telemetry;

public sealed class TelemetryTests : AgentTestBase
{
    // One listener for every test of the class, listening before the first test runs; each test finds its run by
    // its conversation
    private static readonly RecordedActivities Activities;

    static TelemetryTests() => Activities = new RecordedActivities();

    [Fact]
    public async Task Given_A_Run_It_Is_An_Invoke_Agent_Span_Around_Its_Chat_And_Tool_Spans()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync();

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        run.DisplayName.Should().Be("invoke_agent probe_agent");
        Activities.Inside(run).Select(activity => activity.DisplayName)
            .Should()
            .Equal($"chat {ProbeAgent.Model}", "execute_tool probe", $"chat {ProbeAgent.Model}");
    }

    [Fact]
    public async Task Given_A_Run_Its_Span_Carries_The_Token_Usage()
    {
        // Arrange
        var usage = new UsageDetails
        {
            InputTokenCount = Data.Agent.InputTokens,
            OutputTokenCount = Data.Agent.OutputTokens
        };
        ChatClient.Reply(
            Text(Data.Tool.Answer),
            new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(usage)]));

        // Act
        await RunAsync();

        // Assert
        var run = Activities.Run(Data.Agent.ConversationId);
        run.GetTagItem("gen_ai.usage.input_tokens").Should().Be(Data.Agent.InputTokens);
        run.GetTagItem("gen_ai.usage.output_tokens").Should().Be(Data.Agent.OutputTokens);
    }

    [Fact]
    public async Task Given_A_Failing_Run_Its_Span_Carries_The_Error()
    {
        // Arrange
        ChatClient.Reply(new InvalidOperationException(Data.Agent.Reason));

        // Act
        var run = () => RunAsync();

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>();
        var span = Activities.Run(Data.Agent.ConversationId);
        span.Status.Should().Be(ActivityStatusCode.Error);
        span.GetTagItem("error.type").Should().Be(typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public async Task Given_A_Store_That_Fails_To_Append_The_Runs_Span_Records_The_Exception()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<FailingAppendStore>().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .RunAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct);

        // Assert
        Activities.Run(Data.Agent.ConversationId).Events.Select(@event => @event.Name).Should().Contain("exception");
    }

    [Fact]
    public async Task Given_Two_Runs_On_One_Provider_Client_Their_Chat_Spans_Come_From_One_Source()
    {
        // Arrange
        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync();
        await RunAsync();

        // Assert
        Activities.Runs(Data.Agent.ConversationId)
            .SelectMany(Activities.Inside)
            .Select(activity => activity.Source)
            .Distinct()
            .Should()
            .ContainSingle();
    }

    private Task<ChatResponse> RunAsync() =>
        Host<ProbeAgent>().RunAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct);
}
