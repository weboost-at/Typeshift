using Typeshift.Tests.Agents.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Conversations;

// D73: with every conversation kept, a run without a conversation id starts one, and the response names it
public class PersistAllConversationsTests : AgentTestBase
{
    [Fact]
    public async Task Given_Every_Conversation_Kept_A_Run_Without_An_Id_Stores_One_And_The_Response_Names_It()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        (await EntriesAsync(Guid.Parse(response.ConversationId!))).Should().NotBeEmpty();
    }

    [Fact]
    public async Task Given_The_Id_A_Response_Names_The_Next_Run_With_It_Resumes_The_Conversation()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer)).Reply(Text(Data.Tool.Answer));

        var first = await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await Host<ProbeAgent>()
            .WithConversationId(Guid.Parse(first.ConversationId!))
            .RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        ChatClient.Requests[1].Select(message => message.Text)
            .Should().Equal(Data.Tool.Question, Data.Tool.Answer, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_Every_Conversation_Kept_Two_Runs_Without_An_Id_Start_A_Conversation_Each()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer)).Reply(Text(Data.Tool.Answer));

        var first = await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        var second = await Host<ProbeAgent>().RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        second.ConversationId.Should().NotBe(first.ConversationId);
    }

    [Fact]
    public async Task Given_Every_Conversation_Kept_A_Streaming_Run_Without_An_Id_Names_Its_Conversation()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var updates = await Host<ProbeAgent>().RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct);

        // Assert
        (await EntriesAsync(Guid.Parse(updates[^1].ConversationId!))).Should().NotBeEmpty();
    }

    [Fact]
    public async Task Given_A_Streaming_Run_That_Calls_A_Tool_The_Next_Model_Call_Still_Gets_The_Whole_History()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct);

        // Assert
        ChatClient.Requests[1][0].Text.Should().Be(Data.Tool.Question);
    }

    [Fact]
    public async Task Given_A_Run_With_A_Conversation_Id_The_Response_Names_That_Conversation()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ProbeAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.ConversationId.Should().Be(Data.Agent.ConversationId.ToString());
    }

    [Fact]
    public async Task Given_New_Conversation_Ids_Of_Another_Type_A_New_Conversation_Is_Stored_Under_One()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var services = new ServiceCollection();
        services.AddTypeshift()
            .WithStore<InMemoryTypeshiftStore<int>>()
            .WithOptions(options =>
            {
                options.PersistAllConversations = true;
                options.NewConversationIds(() => Data.Persistence.NumberId);
            })
            .WithChatClientProvider<ScriptedChatClientProvider>();

        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();

        // Act
        await scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        var entries = await provider.GetRequiredService<IConversationStore<int>>()
            .LoadConversationAsync(Data.Persistence.NumberId, Ct);

        entries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Given_Conversations_Kept_Only_By_Id_A_Run_Without_One_Names_No_Conversation()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var services = new ServiceCollection();
        services.AddTypeshift()
            .WithStore<InMemoryTypeshiftStore>()
            .WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var response = await scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.ConversationId.Should().BeNull();
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddTypeshift().WithOptions(options => options.PersistAllConversations = true);
    }
}
