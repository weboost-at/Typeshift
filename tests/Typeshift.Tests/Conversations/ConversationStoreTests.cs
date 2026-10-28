using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Compaction.Support;
using Typeshift.Tests.Conversations.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Conversations;

public class ConversationStoreTests : AgentTestBase
{
    [Fact]
    public async Task Given_A_Summary_The_Store_Loads_It_And_The_Entries_After_It()
    {
        // Arrange
        ITypeshiftStore store = new InMemoryTypeshiftStore();
        var conversation = Data.Agent.ConversationId;

        ConversationEntry[] entries =
        [
            new("probe_agent", [new ChatMessage(ChatRole.User, Data.Tool.Question)], Time.GetUtcNow()),
            new("probe_agent", [new ChatMessage(ChatRole.User, Data.Agent.Reason)], Time.GetUtcNow())
            {
                IsSummary = true
            },
            new("probe_agent", [new ChatMessage(ChatRole.User, Data.Agent.FollowUp)], Time.GetUtcNow())
        ];

        foreach (var entry in entries)
        {
            await store.AppendConversationAsync(conversation, entry, Ct);
        }

        // Act
        var loaded = await store.LoadConversationSinceCompactionAsync(conversation, Ct);

        // Assert
        loaded.Select(entry => entry.Messages[0].Text).Should().Equal(Data.Agent.Reason, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_A_Summary_The_Next_Run_Never_Loads_The_Whole_Conversation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<CountingLoadStore>().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<CompactingAgent>>();
        var settings = new CompactingAgent.Settings { MaxInputTokens = 1_000 };

        ChatClient
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        await host.RunAsync(new string('x', Page), conversationId: Data.Agent.ConversationId, ct: Ct);
        await host.RunAsync(Data.Agent.FollowUp, conversationId: Data.Agent.ConversationId, settings: settings, ct: Ct);

        // Act
        await host.RunAsync(Data.Agent.Steering, conversationId: Data.Agent.ConversationId, ct: Ct);

        // Assert
        provider.GetRequiredService<CountingLoadStore>().WholeLoads.Should().Be(0);
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
