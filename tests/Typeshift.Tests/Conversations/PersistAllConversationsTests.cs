using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Conversations.Support;
using Typeshift.Tests.MultiAgent.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Conversations;

// D73: with every conversation kept, a run without a conversation id starts one, and the response names it
public class PersistAllConversationsTests : MultiAgentTestBase
{
    private RecordingStore Store { get; } = new();

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
    public async Task Given_No_NewConversationIds_A_New_Conversation_Id_Is_A_Guid_Of_Version_7_At_The_Clocks_Time()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);
        var conversation = Guid.Parse(response.ConversationId!);

        // Assert
        conversation.Version.Should().Be(7);
        Convert.ToInt64(conversation.ToString("N")[..12], 16).Should().Be(Time.GetUtcNow().ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task Given_No_NewConversationIds_And_No_Store_Of_Guid_Conversations_A_Run_Fails_Naming_Them()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift()
            .WithStore<InMemoryTypeshiftStore<int>>()
            .WithOptions(options => options.PersistAllConversations = true)
            .WithChatClientProvider<ScriptedChatClientProvider>();

        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var run = () => scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No IConversationStore<Guid> is registered*NewConversationIds*");
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
    public async Task Given_Every_Conversation_Kept_A_Raw_Call_Without_Input_Starts_No_Conversation()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>();
        var options = await host.ResolveOptionsAsync(ct: Ct);
        using var client = await host.CreateChatClientAsync(options, Ct);

        // Act
        await client.GetResponseAsync([], options, Ct);

        // Assert
        Store.Conversations.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Every_Conversation_Kept_A_Raw_Call_With_Options_Of_Its_Own_Starts_No_Conversation()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>();

        var options = new ChatOptions
        {
            ModelId = ProbeAgent.Model
        };

        using var client = await host.CreateChatClientAsync(options, Ct);

        // Act
        await client.GetResponseAsync(Data.Tool.Question, options, Ct);

        // Assert
        Store.Conversations.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_The_Id_A_Raw_Response_Names_A_Raw_Call_With_It_Resumes_The_Conversation()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer)).Reply(Text(Data.Tool.Answer));

        await using var provider = KeepingStringConversations();
        await using var scope = provider.CreateAsyncScope();

        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>();
        var options = await host.ResolveOptionsAsync(ct: Ct);
        using var client = await host.CreateChatClientAsync(options, Ct);

        var first = await client.GetResponseAsync(Data.Tool.Question, options, Ct);
        options.ConversationId = first.ConversationId;

        // Act
        await client.GetResponseAsync(Data.Agent.FollowUp, options, Ct);

        // Assert
        ChatClient.Requests[1].Select(message => message.Text)
            .Should().Equal(Data.Tool.Question, Data.Tool.Answer, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_Every_Conversation_Kept_A_Raw_Call_Stores_One_Under_The_Text_Of_A_Guid_Of_Version_7()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        await using var provider = KeepingStringConversations();
        await using var scope = provider.CreateAsyncScope();

        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>();
        var options = await host.ResolveOptionsAsync(ct: Ct);
        using var client = await host.CreateChatClientAsync(options, Ct);

        // Act
        var response = await client.GetResponseAsync(Data.Tool.Question, options, Ct);

        // Assert
        var entries = await provider.GetRequiredService<IConversationStore<string>>()
            .LoadConversationAsync(response.ConversationId!, Ct);

        Guid.Parse(response.ConversationId!).Version.Should().Be(7);
        entries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Given_New_Conversation_Ids_Of_Strings_A_Raw_Call_Stores_Its_Conversation_Under_One()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        await using var provider = KeepingStringConversations(options => options.NewConversationIds(
            () => Data.Persistence.ChatName));
        await using var scope = provider.CreateAsyncScope();

        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>();
        var options = await host.ResolveOptionsAsync(ct: Ct);
        using var client = await host.CreateChatClientAsync(options, Ct);

        // Act
        var response = await client.GetResponseAsync(Data.Tool.Question, options, Ct);

        // Assert
        response.ConversationId.Should().Be(Data.Persistence.ChatName);
    }

    [Fact]
    public async Task Given_New_Conversation_Ids_Of_Ints_A_Raw_Call_Stores_Its_Conversation_Under_The_Text_Of_A_Guid()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        await using var provider = KeepingStringConversations(options => options.NewConversationIds(
            () => Data.Persistence.NumberId));
        await using var scope = provider.CreateAsyncScope();

        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>();
        var options = await host.ResolveOptionsAsync(ct: Ct);
        using var client = await host.CreateChatClientAsync(options, Ct);

        // Act
        var response = await client.GetResponseAsync(Data.Tool.Question, options, Ct);

        // Assert
        Guid.Parse(response.ConversationId!).Version.Should().Be(7);
    }

    [Fact]
    public async Task Given_Every_Conversation_Kept_A_Sub_Agents_Run_Stores_Only_Its_Callers_Conversation()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(Text(Data.Agent.Reason));

        // Act
        var response = await Host<LeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Store.Conversations.Should().Equal(Guid.Parse(response.ConversationId!));
    }

    [Fact]
    public async Task Given_Every_Conversation_Kept_A_Handoff_Answers_In_The_Callers_New_Conversation()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<FrontAgent>().RunAsync(Data.Tool.Question, ct: Ct);
        var conversation = Guid.Parse(response.ConversationId!);

        // Assert
        Store.Conversations.Should().Equal(conversation);
        (await EntriesAsync(conversation))[^1].Agent.Should().Be("back_agent");
    }

    [Fact]
    public async Task Given_Every_Conversation_Kept_A_Streamed_Handoff_Answers_In_The_Callers_New_Conversation()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var updates = await Host<FrontAgent>().RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct);
        var conversation = Guid.Parse(updates[^1].ConversationId!);

        // Assert
        Store.Conversations.Should().Equal(conversation);
        (await EntriesAsync(conversation))[^1].Agent.Should().Be("back_agent");
    }

    [Fact]
    public async Task Given_Conversations_Kept_Only_By_Id_A_Run_Without_One_Names_No_Conversation()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var services = new ServiceCollection();
        services.AddTypeshift()
            .WithStore<InMemoryTypeshiftStore<Guid>>()
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

    // D73: every conversation kept, in a store of string ids, which the raw path names its conversations by
    private ServiceProvider KeepingStringConversations(Action<TypeshiftOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddTypeshift()
            .WithStore<InMemoryTypeshiftStore<string>>()
            .WithOptions(options => options.PersistAllConversations = true)
            .WithOptions(options => configure?.Invoke(options))
            .WithChatClientProvider<ScriptedChatClientProvider>();

        services.AddSingleton(ChatClient).AddSingleton(Models);
        return services.BuildServiceProvider(validateScopes: true);
    }

    // The store registered last serves the Guid ids, so a test sees each conversation a run stored
    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddTypeshift().WithStore(Store).WithOptions(options => options.PersistAllConversations = true);
    }
}
