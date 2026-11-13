using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Testing.Fakes;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Typeshift.Tests.Persistence;

// D72: ids given in code, whose types pick the stores, beside the Guid store of the base
public class IdScopeTests : AgentTestBase
{
    [Fact]
    public async Task Given_Settings_Under_An_Int_Id_A_Run_With_The_Id_Applies_Them()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>().WithSettingsId(Data.Persistence.NumberId);
        await host.ConfigureAsync(settings => settings with { Temperature = Data.Agent.Temperature }, Ct);

        // Act
        await host.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ChatClient.Options[0]!.Temperature.Should().Be(Data.Agent.Temperature);
    }

    [Fact]
    public async Task Given_Settings_Configured_Under_An_Id_They_Are_In_The_Store_Of_Its_Type()
    {
        // Arrange
        var host = Host<ProbeAgent>().WithSettingsId(Data.Persistence.NumberId);

        // Act
        await host.ConfigureAsync(settings => settings with { Temperature = Data.Agent.Temperature }, Ct);

        // Assert
        var stored = await Services.GetRequiredService<ISettingsStore<int>>()
            .LoadAsync(Data.Persistence.NumberId, "ProbeAgent", Ct);

        stored.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_Settings_Under_An_Id_The_Host_It_Came_From_Keeps_The_Agents_Own()
    {
        // Arrange
        var host = Host<ProbeAgent>();
        await host.WithSettingsId(Data.Persistence.NumberId).ConfigureAsync(
            settings => settings with { Temperature = Data.Agent.Temperature },
            Ct);

        // Act
        var own = await host.LoadSettingsAsync(Ct);

        // Assert
        own.Version.Should().Be(0);
    }

    [Fact]
    public async Task Given_A_Conversation_Under_A_String_Id_The_Next_Run_Resumes_It()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer)).Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>().WithConversationId(Data.Persistence.ChatName);
        await host.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await host.RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        ChatClient.Requests[1].Select(message => message.Text)
            .Should().Equal(Data.Tool.Question, Data.Tool.Answer, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_Conversations_Of_Two_Id_Types_With_The_Same_Text_Each_Is_A_Conversation_Of_Its_Own()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer)).Reply(Text(Data.Tool.Answer));

        await Host<ProbeAgent>().WithConversationId(Data.Persistence.NumberId).RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await Host<ProbeAgent>()
            .WithConversationId(Data.Persistence.NumberId.ToString())
            .RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        ChatClient.Requests[1].Select(message => message.Text).Should().Equal(Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_A_Run_Of_A_Conversation_Under_An_Int_Id_The_Guid_Tracker_Leaves_It_Out()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        ChatClient.Reply(gate, Text(Data.Tool.Answer));

        var run = Host<ProbeAgent>().WithConversationId(Data.Persistence.NumberId).RunAsync(Data.Tool.Question, ct: Ct);
        await ChatClient.Requested;

        // Act
        var runs = Tracker.Runs;

        // Assert
        runs.Should().BeEmpty();
        gate.SetResult();
        await run;
    }

    [Fact]
    public async Task Given_A_Run_Of_A_Conversation_Under_An_Int_Id_The_Int_Tracker_Lists_It()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        ChatClient.Reply(gate, Text(Data.Tool.Answer));

        var run = Host<ProbeAgent>().WithConversationId(Data.Persistence.NumberId).RunAsync(Data.Tool.Question, ct: Ct);
        await ChatClient.Requested;

        // Act
        var runs = Services.GetRequiredService<ConversationTracker<int>>().Runs;

        // Assert
        runs.Select(live => live.Conversation).Should().Equal(Data.Persistence.NumberId);
        gate.SetResult();
        await run;
    }

    [Fact]
    public async Task Given_An_Id_Of_A_Type_No_Store_Keeps_The_Run_Fails_Naming_The_Store_It_Needs()
    {
        // Arrange
        var host = Host<ProbeAgent>().WithSettingsId((long)Data.Persistence.NumberId);

        // Act
        var run = () => host.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No ISettingsStore<long> is registered*");
    }

    [Fact]
    public async Task Given_No_Store_A_Conversation_Under_An_Id_Of_Any_Type_Is_Kept_In_Memory()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer)).Reply(Text(Data.Tool.Answer));

        var services = new ServiceCollection();
        services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();

        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Persistence.NumberId);

        await host.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await host.RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        ChatClient.Requests[1].Select(message => message.Text)
            .Should().Equal(Data.Tool.Question, Data.Tool.Answer, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_A_Host_With_A_Settings_Id_Its_Saves_And_Loads_Take_The_Ids_Settings()
    {
        // Arrange
        var host = Host<ProbeAgent>().WithSettingsId(Data.Persistence.NumberId);
        await host.SaveSettingsAsync(new() { Temperature = Data.Agent.Temperature }, 0, Ct);

        // Act
        var stored = await host.LoadSettingsAsync(Ct);

        // Assert
        stored.Data.Temperature.Should().Be(Data.Agent.Temperature);
        (await Host<ProbeAgent>().LoadSettingsAsync(Ct)).Version.Should().Be(0);
    }

    [Fact]
    public async Task Given_A_Host_With_A_Conversation_The_Raw_Path_Stores_The_Run_In_It()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>().WithConversationId(Data.Persistence.ChatName);
        var options = await host.ResolveOptionsAsync(ct: Ct);
        using var client = await host.CreateChatClientAsync(options, Ct);

        // Act
        await client.GetResponseAsync(Data.Tool.Question, options, Ct);

        // Assert
        var entries = await Services.GetRequiredService<IConversationStore<string>>()
            .LoadConversationAsync(Data.Persistence.ChatName, Ct);

        entries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Given_A_Conversation_Id_In_The_Options_The_Raw_Path_Stores_The_Run_Under_It_As_A_String()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>();
        var options = await host.ResolveOptionsAsync(ct: Ct);
        options.ConversationId = Data.Persistence.ChatName;

        // Act
        using var client = await host.CreateChatClientAsync(options, Ct);
        await client.GetResponseAsync([new(ChatRole.User, Data.Tool.Question)], options, Ct);

        // Assert
        var entries = await Services.GetRequiredService<IConversationStore<string>>()
            .LoadConversationAsync(Data.Persistence.ChatName, Ct);

        entries.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_An_Interrupted_Run_Of_The_Hosts_Conversation_Resuming_Continues_It()
    {
        // Arrange
        var store = Services.GetRequiredService<IConversationStore<string>>();
        var input = new ChatMessage(ChatRole.User, Data.Tool.Question);
        var call = new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(Data.Tool.CallId, "probe")]);

        var conversation = Data.Persistence.ChatName;

        await store.AppendConversationAsync(conversation, new("probe_agent", [input], Time.GetUtcNow()), Ct);
        await store.AppendConversationAsync(conversation, new("probe_agent", [call], Time.GetUtcNow()), Ct);

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ProbeAgent>().WithConversationId(Data.Persistence.ChatName).ResumeAsync(ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Host_Without_A_Conversation_Resuming_Fails_Naming_WithConversationId()
    {
        // Act
        var resume = () => Host<ProbeAgent>().ResumeAsync(ct: Ct);

        // Assert
        await resume.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WithConversationId*");
    }

    [Fact]
    public async Task Given_A_Run_Of_A_Conversation_Under_An_Int_Id_The_Int_Tracker_Steers_It()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        ChatClient.Reply(gate, Text(Data.Tool.Answer)).Reply(Text(Data.Tool.Answer));

        var run = Host<ProbeAgent>().WithConversationId(Data.Persistence.NumberId).RunAsync(Data.Tool.Question, ct: Ct);
        await ChatClient.Requested;

        // Act
        var steered = Services.GetRequiredService<ConversationTracker<int>>()
            .TrySteer(Data.Persistence.NumberId, Data.Agent.FollowUp);

        // Assert
        steered.Should().BeTrue();
        gate.SetResult();
        await run;
    }

    [Fact]
    public async Task Given_A_Store_Your_App_Created_Runs_With_Ids_Of_Its_Type_Use_It()
    {
        // Arrange
        var store = new InMemoryTypeshiftStore<int>();
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore(store).WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();

        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithSettingsId(Data.Persistence.NumberId);

        // Act
        await host.ConfigureAsync(settings => settings with { Temperature = Data.Agent.Temperature }, Ct);

        // Assert
        (await store.LoadAsync(Data.Persistence.NumberId, "ProbeAgent", Ct)).Should().NotBeNull();
    }

    [Fact]
    public async Task Given_A_Store_Registered_Without_WithStore_A_Save_To_It_Logs_No_Memory_Warning()
    {
        // Arrange
        var logs = new WarningLog();
        var services = new ServiceCollection();
        services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton<ISettingsStore<int>>(new InMemoryTypeshiftStore<int>());
        services.AddSingleton(ChatClient).AddSingleton(Models).AddSingleton<ILoggerFactory>(logs);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();

        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithSettingsId(Data.Persistence.NumberId);

        // Act
        await host.ConfigureAsync(settings => settings with { Temperature = Data.Agent.Temperature }, Ct);

        // Assert
        logs.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Store_Registered_Without_WithStore_What_It_Does_Not_Keep_Fails_Naming_The_Store()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton<IConversationStore<Guid>>(new InMemoryTypeshiftStore<Guid>());
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();

        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithSettingsId(Data.Persistence.SettingsId);

        // Act
        var configure = () => host.ConfigureAsync(
            settings => settings with { Temperature = Data.Agent.Temperature },
            Ct);

        // Assert
        await configure.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No ISettingsStore<Guid> is registered*");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_A_Store_Registered_As_Scoped_The_First_Run_Fails_Naming_The_Rule(bool validateScopes)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddScoped<IConversationStore<Guid>, InMemoryTypeshiftStore<Guid>>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider(validateScopes);
        await using var scope = provider.CreateAsyncScope();

        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId);

        // Act
        var run = () => host.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("IConversationStore<Guid> is registered as scoped, but a store is one*WithStore<T>()*");
    }

    [Fact]
    public async Task Given_A_Store_Of_Agent_Settings_Whose_Creation_Failed_Once_The_Next_Run_Resolves_It_Again()
    {
        // Arrange
        var creations = 0;
        var services = new ServiceCollection();
        services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        services.AddSingleton<IAgentSettingsStore>(_ => Interlocked.Increment(ref creations) == 1
            ? throw new TimeoutException(Data.Agent.Reason)
            : new InMemoryTypeshiftStore<Guid>());

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>();

        ChatClient.Reply(Text(Data.Tool.Answer));
        await Record.ExceptionAsync(() => host.RunAsync(Data.Tool.Question, ct: Ct));

        // Act
        var response = await host.RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddTypeshift().WithStore<InMemoryTypeshiftStore<int>>().WithStore<InMemoryTypeshiftStore<string>>();
    }
}
