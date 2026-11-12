using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Hosting.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Hosting;

// D75: Typeshift without a container of the app's own
public class TypeshiftAppTests : AgentTestBase
{
    [Fact]
    public async Task Given_An_App_Over_A_Client_An_Agent_Runs_On_It()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        await using var app = TypeshiftApp.Create(ChatClient);

        // Act
        var response = await app.Agent<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_An_App_With_A_Store_An_Agents_Configured_Settings_Apply_To_Its_Runs()
    {
        // Arrange
        await using var app = TypeshiftApp.Create(
            ChatClient,
            typeshift => typeshift.WithStore<InMemoryTypeshiftStore>());

        await app.Agent<ProbeAgent>().ConfigureAsync(
            Data.Persistence.SettingsId,
            settings => settings with { Temperature = Data.Agent.Temperature },
            Ct);

        // Act
        var options = await app.Agent<ProbeAgent>().ResolveOptionsAsync(Data.Persistence.SettingsId, ct: Ct);

        // Assert
        options.Temperature.Should().Be(Data.Agent.Temperature);
    }

    [Fact]
    public async Task Given_An_App_Disposed_After_A_Run_The_Client_It_Was_Given_Stays_Undisposed()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        var app = TypeshiftApp.Create(ChatClient);
        await app.Agent<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await app.DisposeAsync();

        // Assert
        ChatClient.Disposed.Should().BeFalse();
    }

    [Fact]
    public async Task Given_A_Scope_That_Fails_To_Dispose_The_Apps_Singletons_Are_Still_Disposed()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var app = TypeshiftApp.Create(
            ChatClient,
            typeshift => typeshift
                .WithStore<DisposableStore>()
                .WithRunMiddleware<AsyncOnlyMiddleware>());

        var store = app.Services.GetRequiredService<DisposableStore>();
        await app.Agent<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        var dispose = () => app.Dispose();

        // Assert
        dispose.Should().Throw<InvalidOperationException>();
        store.Disposed.Should().BeTrue();
    }

    [Fact]
    public void Given_No_Client_Creating_An_App_Throws()
    {
        // Act
        var create = () => TypeshiftApp.Create(null!);

        // Assert
        create.Should().Throw<ArgumentNullException>();
    }
}
