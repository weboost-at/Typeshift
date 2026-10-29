using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Conversations.Support;
using Typeshift.Tests.Persistence.Support;
using Typeshift.Tests.Tools.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Persistence;

public class StoredSettingsTests : AgentTestBase
{
    [Fact]
    public async Task Given_Stored_Settings_A_Run_With_Their_Id_Applies_Them()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>();
        var stored = new ProbeAgent.Settings { Provider = Data.Agent.Provider, Model = Data.Agent.Model };
        await host.SaveSettingsAsync(Data.Persistence.SettingsId, stored, 0, Ct);

        // Act
        await host.RunAsync(Data.Tool.Question, Data.Persistence.SettingsId, ct: Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(Data.Agent.Provider, Data.Agent.Model));
    }

    [Fact]
    public async Task Given_Stored_And_Run_Settings_The_Runs_Apply_Over_The_Stored()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>();
        var stored = new ProbeAgent.Settings { Model = Data.Agent.Model, Temperature = 0.1f };
        await host.SaveSettingsAsync(Data.Persistence.SettingsId, stored, 0, Ct);

        // Act
        await host.RunAsync(
            Data.Tool.Question,
            Data.Persistence.SettingsId,
            settings: new() { Temperature = Data.Agent.Temperature },
            ct: Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(ProbeAgent.ModelProvider, Data.Agent.Model));
        ChatClient.Options[0]!.Temperature.Should().Be(Data.Agent.Temperature);
    }

    [Fact]
    public async Task Given_Stored_Tool_Overrides_The_Tool_Runs_With_Them_Over_The_Agents_Settings()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>();
        var stored = new ProbeAgent.Settings { Probe = new() { Limit = Data.Tool.Limit } };
        await host.SaveSettingsAsync(Data.Persistence.SettingsId, stored, 0, Ct);

        // Act
        var response = await host.RunAsync(Data.Tool.Question, Data.Persistence.SettingsId, ct: Ct);

        // Assert
        response.Assert.ToolResult<ProbeResult>(Data.Tool.CallId, Json).Settings
            .Should()
            .Be(new ProbeSettings { Limit = Data.Tool.Limit, Verbose = true });
    }

    [Fact]
    public async Task Given_No_Stored_Settings_Loading_Returns_Empty_Settings_At_Version_Zero()
    {
        // Act
        var stored = await Host<ProbeAgent>().LoadSettingsAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        stored.Version.Should().Be(0);
        stored.Data.Should().BeEquivalentTo(new ProbeAgent.Settings());
    }

    [Fact]
    public async Task Given_A_Save_Loading_Returns_The_Settings_With_Its_Version()
    {
        // Arrange
        var host = Host<ProbeAgent>();
        var settings = new ProbeAgent.Settings { Temperature = Data.Agent.Temperature };

        // Act
        var version = await host.SaveSettingsAsync(Data.Persistence.SettingsId, settings, 0, Ct);
        var stored = await host.LoadSettingsAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        stored.Should().BeEquivalentTo(new Stored<ProbeAgent.Settings>(settings, version));
    }

    [Fact]
    public async Task Given_One_Id_For_Two_Agents_The_Store_Keeps_The_Json_Of_Each_Under_Its_Kind()
    {
        // Arrange
        var store = Services.GetRequiredService<ITypeshiftStore>();
        var probe = new ProbeAgent.Settings { Model = Data.Agent.Model };

        // Act
        await Host<ProbeAgent>().SaveSettingsAsync(Data.Persistence.SettingsId, probe, 0, Ct);
        await Host<EveryKindAgent>().SaveSettingsAsync(Data.Persistence.SettingsId, Data.Persistence.EveryKind, 0, Ct);

        var probeDocument = await store.LoadAsync(Data.Persistence.SettingsId, ProbeAgent.Settings.Kind, Ct);
        var everyKindDocument = await store.LoadAsync(Data.Persistence.SettingsId, EveryKindAgent.Settings.Kind, Ct);

        // Assert
        probeDocument!.Json.Should().Be(probe.Serialize());
        everyKindDocument!.Json.Should().Be(Data.Persistence.EveryKind.Serialize());
    }

    [Fact]
    public async Task Given_A_Version_Another_Save_Replaced_Saving_Throws_A_StoreConflictException()
    {
        // Arrange
        var host = Host<ProbeAgent>();
        await host.SaveSettingsAsync(Data.Persistence.SettingsId, new(), 0, Ct);

        // Act
        var save = () => host.SaveSettingsAsync(Data.Persistence.SettingsId, new(), 0, Ct);

        // Assert
        await save.Should().ThrowAsync<StoreConflictException>();
    }

    [Fact]
    public async Task Given_A_Stale_Version_Saving_Throws_A_StoreConflictException()
    {
        // Arrange
        var host = Host<ProbeAgent>();
        var stale = await host.SaveSettingsAsync(Data.Persistence.SettingsId, new(), 0, Ct);
        await host.SaveSettingsAsync(Data.Persistence.SettingsId, new(), stale, Ct);

        // Act
        var save = () => host.SaveSettingsAsync(Data.Persistence.SettingsId, new(), stale, Ct);

        // Assert
        await save.Should().ThrowAsync<StoreConflictException>();
    }

    [Fact]
    public async Task Given_A_Save_Between_Load_And_Save_Configuring_Applies_The_Change_Again_And_Wins()
    {
        // Arrange
        var host = Host<ProbeAgent>();
        var calls = 0;

        async Task<ProbeAgent.Settings> Interrupt(ProbeAgent.Settings settings)
        {
            if (calls++ == 0)
                await host.SaveSettingsAsync(Data.Persistence.SettingsId, new() { Model = Data.Agent.Model }, 0, Ct);

            return settings with { Temperature = Data.Agent.Temperature };
        }

        // Act
        await host.ConfigureAsync(
            Data.Persistence.SettingsId,
            settings => Interrupt(settings).GetAwaiter().GetResult(),
            Ct);

        var stored = await host.LoadSettingsAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        calls.Should().Be(2);
        stored.Data.Should().BeEquivalentTo(new ProbeAgent.Settings
        {
            Model = Data.Agent.Model,
            Temperature = Data.Agent.Temperature
        });
    }

    [Fact]
    public async Task Given_A_Save_Between_Every_Load_And_Save_Configuring_Gives_Up_After_Ten_Attempts()
    {
        // Arrange
        var host = Host<ProbeAgent>();
        var calls = 0;

        async Task<ProbeAgent.Settings> Interrupt(ProbeAgent.Settings settings)
        {
            calls++;
            var stored = await host.LoadSettingsAsync(Data.Persistence.SettingsId, Ct);
            await host.SaveSettingsAsync(Data.Persistence.SettingsId, stored.Data, stored.Version, Ct);

            return settings with { Temperature = Data.Agent.Temperature };
        }

        // Act
        var configure = () => host.ConfigureAsync(
            Data.Persistence.SettingsId,
            settings => Interrupt(settings).GetAwaiter().GetResult(),
            Ct);

        // Assert
        await configure.Should().ThrowAsync<StoreConflictException>();
        calls.Should().Be(10);
    }

    [Fact]
    public async Task Given_No_Store_A_Run_With_A_Settings_Id_Fails_Naming_WithStore()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>();

        // Act
        var run = () => host.RunAsync(Data.Tool.Question, Data.Persistence.SettingsId, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WithStore*");
    }

    [Fact]
    public async Task Given_A_Store_Of_Settings_Alone_A_Run_With_Their_Id_Applies_Them()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<SettingsOnlyStore>().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>();

        var stored = new ProbeAgent.Settings
        {
            Provider = Data.Agent.Provider,
            Model = Data.Agent.Model
        };

        ChatClient.Reply(Text(Data.Tool.Answer));
        await host.SaveSettingsAsync(Data.Persistence.SettingsId, stored, 0, Ct);

        // Act
        await host.RunAsync(Data.Tool.Question, Data.Persistence.SettingsId, ct: Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(Data.Agent.Provider, Data.Agent.Model));
    }

    [Fact]
    public async Task Given_A_Store_Of_Conversations_Alone_Loading_Settings_Fails_Naming_ISettingsStore()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<CountingLoadStore>().WithChatClientProvider<ScriptedChatClientProvider>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>();

        // Act
        var load = () => host.LoadSettingsAsync(Data.Persistence.SettingsId, Ct);

        // Assert
        await load.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ISettingsStore*");
    }
}
