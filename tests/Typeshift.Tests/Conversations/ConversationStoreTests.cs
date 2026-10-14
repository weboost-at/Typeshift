using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Conversations.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Conversations;

public class ConversationStoreTests : AgentTestBase
{
    [Fact]
    public void Given_An_Entry_With_Every_Member_Set_It_Reads_Back_The_Same()
    {
        // Arrange
        var entry = new ConversationEntry(
            "probe_agent",
            [
                new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(Data.Tool.CallId, "probe")])
                {
                    AuthorName = "probe_agent"
                },
                new ChatMessage(ChatRole.Tool, [new FunctionResultContent(Data.Tool.CallId, Data.Tool.Answer)])
            ],
            Time.GetUtcNow())
        {
            Model = ProbeAgent.Model,
            Usage = new UsageDetails { InputTokenCount = 12, OutputTokenCount = 3, TotalTokenCount = 15 },
            EndsRun = true,
            Mode = AgentMode.Auto,
            IsSummary = true
        };

        // Act
        var read = ConversationEntry.Deserialize(entry.Serialize(), ConversationEntry.Version);

        // Assert
        read.Serialize().Should().Be(entry.Serialize());
    }

    [Fact]
    public async Task Given_A_Store_That_Fails_To_Append_The_Run_Still_Answers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<FailingAppendStore>().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .RunAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_No_Store_A_Run_With_A_Conversation_Fails_Naming_WithStore()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        // Act
        var run = () => scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .RunAsync(Data.Tool.Question, conversationId: Data.Agent.ConversationId, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WithStore*");
    }

    [Fact]
    public async Task Given_No_Live_Updates_Watching_Fails_Naming_WithRealtimeTracking()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift();

        await using var provider = services.BuildServiceProvider();

        // Act
        var watch = () => provider.GetRequiredService<ConversationTracker>().WatchAsync(Data.Agent.ConversationId, Ct);

        // Assert
        watch.Should().Throw<InvalidOperationException>().WithMessage("*WithRealtimeTracking*");
    }
}
