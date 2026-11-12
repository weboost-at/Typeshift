using Typeshift.Tests.Agents.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Persistence;

// D72: ids given in code, whose types pick the stores, beside the build's Guid store of the base
public class IdScopeTests : AgentTestBase
{
    [Fact]
    public async Task Given_Settings_Under_An_Id_Of_Another_Type_Than_The_Builds_A_Run_With_The_Id_Applies_Them()
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
    public async Task Given_A_Conversation_Under_An_Id_Of_Another_Type_The_Next_Run_Resumes_It()
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
    public async Task Given_A_Run_Of_A_Conversation_Of_Another_Id_Type_The_Builds_Tracker_Leaves_It_Out()
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
    public async Task Given_A_Settings_Id_From_WithSettingsId_A_Run_Passing_Another_Fails_Naming_Both()
    {
        // Arrange
        var host = Host<ProbeAgent>().WithSettingsId(Data.Persistence.NumberId);

        // Act
        var run = () => host.RunAsync(Data.Tool.Question, Data.Persistence.SettingsId, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<ArgumentException>().WithMessage("*WithSettingsId*settingsId*");
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

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddTypeshift().WithStore<InMemoryTypeshiftStore<int>>().WithStore<InMemoryTypeshiftStore<string>>();
    }
}
