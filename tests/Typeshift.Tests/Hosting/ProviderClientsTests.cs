using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Hosting.Support;
using Typeshift.Tests.MultiAgent.Support;
using Typeshift.Tests.Testing.Fakes;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Hosting;

// D88: the clients Typeshift keeps for the provider documents runs are handed, and when it disposes them
public class ProviderClientsTests : AgentTestBase
{
    private static readonly TimeSpan IdleTime = TimeSpan.FromMinutes(10);

    private Connections Connections { get; } = [];

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddTypeshift().WithProviderConnector<CountingConnector>();
        services.AddSingleton(Connections);
    }

    [Fact]
    public async Task Given_A_Client_No_Run_Used_For_The_Idle_Time_The_Next_Run_Of_A_Document_Disposes_It()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer)).Reply(Text(Data.Tool.Answer));
        await RunAsync(Document(Data.Agent.Provider));
        Time.Advance(IdleTime);

        // Act
        await RunAsync(Document(Data.Agent.Model));

        // Assert
        Connections[0].Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task Given_A_Client_Unused_For_Less_Than_The_Idle_Time_It_Stays()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer)).Reply(Text(Data.Tool.Answer));
        await RunAsync(Document(Data.Agent.Provider));
        Time.Advance(IdleTime - TimeSpan.FromMinutes(1));

        // Act
        await RunAsync(Document(Data.Agent.Model));

        // Assert
        Connections[0].Disposed.Should().BeFalse();
    }

    [Fact]
    public async Task Given_A_Client_A_Run_Holds_Past_The_Idle_Time_It_Stays()
    {
        // Arrange
        var answering = new TaskCompletionSource();
        ChatClient.Reply(answering, Text(Data.Tool.Answer)).Reply(Text(Data.Tool.Answer));

        var holding = RunAsync(Document(Data.Agent.Provider));
        await ChatClient.Requested.WaitAsync(Ct);
        Time.Advance(IdleTime);

        // Act
        await RunAsync(Document(Data.Agent.Model));
        answering.SetResult();
        await holding;

        // Assert
        Connections[0].Disposed.Should().BeFalse();
    }

    [Fact]
    public async Task Given_An_Idle_Time_Of_The_Apps_Own_A_Client_Is_Disposed_After_It()
    {
        // Arrange
        await using var runtime = TypeshiftRuntime.Create(ChatClient, typeshift =>
        {
            typeshift.WithProviderConnector<CountingConnector>()
                .WithOptions(options => options.ProviderClientIdleTime = TimeSpan.FromMinutes(2));

            typeshift.Services.AddSingleton<TimeProvider>(Time).AddSingleton(Connections).AddSingleton(ChatClient);
        });

        ChatClient.Reply(Text(Data.Tool.Answer)).Reply(Text(Data.Tool.Answer));
        await RunAsync(runtime, Document(Data.Agent.Provider));
        Time.Advance(TimeSpan.FromMinutes(2));

        // Act
        await RunAsync(runtime, Document(Data.Agent.Model));

        // Assert
        Connections[0].Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task Given_A_Runtime_Disposed_The_Clients_It_Kept_Are_Disposed()
    {
        // Arrange
        var runtime = TypeshiftRuntime.Create(ChatClient, typeshift =>
        {
            typeshift.WithProviderConnector<CountingConnector>();
            typeshift.Services.AddSingleton(Connections).AddSingleton(ChatClient);
        });

        ChatClient.Reply(Text(Data.Tool.Answer));
        await RunAsync(runtime, Document(Data.Agent.Provider));

        // Act
        await runtime.DisposeAsync();

        // Assert
        Connections[0].Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task Given_A_Client_The_App_Registered_Typeshift_Never_Disposes_It()
    {
        // Arrange
        var registered = new ConnectedClient(ChatClient, Document(Data.Agent.Provider));

        await using var runtime = TypeshiftRuntime.Create(ChatClient, typeshift =>
        {
            typeshift.WithProviderConnector<CountingConnector>();
            typeshift.Services.AddSingleton<TimeProvider>(Time).AddSingleton(Connections).AddSingleton(ChatClient);
            typeshift.Services.AddKeyedSingleton<IChatClient>(Vendor.Other, registered);
        });

        ChatClient.Reply(Text(Data.Tool.Answer)).Reply(Text(Data.Tool.Answer));
        await using (var host = runtime.CreateHost<ModelOnlyAgent>())
        {
            await host.WithProvider(Vendor.Other).RunAsync(Data.Tool.Question, ct: Ct);
        }

        Time.Advance(IdleTime);

        // Act
        await RunAsync(runtime, Document(Data.Agent.Model));

        // Assert
        registered.Disposed.Should().BeFalse();
    }

    [Fact]
    public async Task Given_A_Run_That_Calls_A_Sub_Agent_On_A_Document_Both_Share_One_Client()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<LeadAgent>().WithProvider(Document(Data.Agent.Provider)).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Connections.Should().ContainSingle();
    }

    private static ChatProvider Document(string key) => new() { Kind = "scripted", ApiKey = key };

    private Task<ChatResponse> RunAsync(ChatProvider provider) =>
        Host<ModelOnlyAgent>().WithProvider(provider).RunAsync(Data.Tool.Question, ct: Ct);

    private async Task RunAsync(ITypeshiftRuntime runtime, ChatProvider provider)
    {
        await using var host = runtime.CreateHost<ModelOnlyAgent>();
        await host.WithProvider(provider).RunAsync(Data.Tool.Question, ct: Ct);
    }
}
