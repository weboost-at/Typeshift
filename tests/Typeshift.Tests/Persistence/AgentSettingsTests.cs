using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Persistence.Support;
using Typeshift.Tests.Testing.Fakes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Typeshift.Tests.Persistence;

public class AgentSettingsTests : AgentTestBase
{
    [Fact]
    public async Task Given_The_Agents_Own_Settings_A_Run_Without_A_Settings_Id_Applies_Them()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>();
        await host.UpdateSettingsAsync(
            settings => settings with { Provider = Data.Agent.Provider, Model = Data.Agent.Model },
            Ct);

        // Act
        await host.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(Data.Agent.Provider, Data.Agent.Model));
    }

    [Fact]
    public async Task Given_The_Agents_Own_Settings_And_Those_Of_A_Settings_Id_The_Ids_Apply_Over_Them()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var host = Host<ProbeAgent>();
        var own = new ProbeAgent.Settings { Model = Data.Agent.Model, Temperature = 0.1f };
        var customer = new ProbeAgent.Settings { Temperature = Data.Agent.Temperature };

        await host.SaveSettingsAsync(own, 0, Ct);
        await host.WithSettingsId(Data.Persistence.SettingsId).SaveSettingsAsync(customer, 0, Ct);

        // Act
        await host.WithSettingsId(Data.Persistence.SettingsId).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(ProbeAgent.ModelProvider, Data.Agent.Model));
        ChatClient.Options[0]!.Temperature.Should().Be(Data.Agent.Temperature);
    }

    [Fact]
    public async Task Given_The_Agents_Own_Settings_Loading_Under_A_Settings_Id_Finds_None()
    {
        // Arrange
        var host = Host<ProbeAgent>();
        await host.UpdateSettingsAsync(settings => settings with { Model = Data.Agent.Model }, Ct);

        // Act
        var stored = await host.WithSettingsId(Data.Persistence.SettingsId).LoadSettingsAsync(Ct);

        // Assert
        stored.Version.Should().Be(0);
    }

    [Fact]
    public async Task Given_A_Save_Under_A_Settings_Id_The_Agents_Own_Settings_Stay_Empty()
    {
        // Arrange
        var host = Host<ProbeAgent>();
        var customer = host.WithSettingsId(Data.Persistence.SettingsId);
        await customer.SaveSettingsAsync(new() { Model = Data.Agent.Model }, 0, Ct);

        // Act
        var own = await host.LoadSettingsAsync(Ct);

        // Assert
        own.Version.Should().Be(0);
    }

    [Fact]
    public async Task Given_A_Null_Settings_Id_Saving_Throws_And_Leaves_The_Agents_Own_Settings_Alone()
    {
        // Arrange
        var host = Host<ProbeAgent>();

        // Act
        var save = () => host.WithSettingsId<string>(null!).SaveSettingsAsync(new(), 0, Ct);

        // Assert
        await save.Should().ThrowAsync<ArgumentNullException>();
        (await host.LoadSettingsAsync(Ct)).Version.Should().Be(0);
    }

    [Fact]
    public async Task Given_The_Agents_Own_Settings_The_Resolved_Options_Hold_Them()
    {
        // Arrange
        var host = Host<ProbeAgent>();
        await host.UpdateSettingsAsync(settings => settings with { Temperature = Data.Agent.Temperature }, Ct);

        // Act
        var options = await host.ResolveOptionsAsync(ct: Ct);

        // Assert
        options.Temperature.Should().Be(Data.Agent.Temperature);
    }

    [Fact]
    public async Task Given_No_Own_Settings_Loading_Them_Returns_Empty_Settings_At_Version_Zero()
    {
        // Act
        var stored = await Host<ProbeAgent>().LoadSettingsAsync(Ct);

        // Assert
        stored.Data.Should().BeEquivalentTo(new ProbeAgent.Settings());
        stored.Version.Should().Be(0);
    }

    [Fact]
    public async Task Given_A_Stale_Version_Saving_The_Agents_Own_Settings_Throws_A_StoreConflictException()
    {
        // Arrange
        var host = Host<ProbeAgent>();
        var first = await host.SaveSettingsAsync(new() { Model = Data.Agent.Model }, 0, Ct);
        await host.SaveSettingsAsync(new() { Temperature = 0.1f }, first, Ct);

        // Act
        var save = () => host.SaveSettingsAsync(new() { Temperature = 0.2f }, first, Ct);

        // Assert
        await save.Should().ThrowAsync<StoreConflictException>();
    }

    [Theory]
    [InlineData(nameof(RunOptions.MaxToolIterations), 0)]
    [InlineData(nameof(RunOptions.MaxToolIterations), -1)]
    [InlineData(nameof(RunOptions.MaxConsecutiveToolErrors), -1)]
    [InlineData(nameof(RunOptions.MaxInputTokens), 0)]
    [InlineData(nameof(RunOptions.MaxInputTokens), -1)]
    [InlineData(nameof(RunOptions.TargetInputTokens), 0)]
    [InlineData(nameof(RunOptions.TargetInputTokens), -1)]
    public async Task Given_Settings_Out_Of_Range_Saving_Throws_And_Stores_Nothing(string option, int value)
    {
        // Arrange
        var host = Host<LimitsAgent>().WithSettingsId(Data.Persistence.SettingsId);

        // Act
        var save = () => host.SaveSettingsAsync(LimitsAgent.Limit(option, value), 0, Ct);

        // Assert
        await save.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage($"*'{LimitsAgent.AgentName}'*{option} {value}*");

        (await host.LoadSettingsAsync(Ct)).Version.Should().Be(0);
    }

    [Fact]
    public async Task Given_A_Target_Not_Below_The_Maximum_Of_The_Same_Settings_Saving_Throws_And_Stores_Nothing()
    {
        // Arrange
        var host = Host<LimitsAgent>();

        // Act
        var save = () => host.SaveSettingsAsync(new() { MaxInputTokens = 1_000, TargetInputTokens = 2_000 }, 0, Ct);

        // Assert
        await save.Should().ThrowAsync<ArgumentException>().WithMessage(
            $"The settings of the agent '{LimitsAgent.AgentName}' set TargetInputTokens 2000, and it must be below "
            + "MaxInputTokens, 1000. No run could go with them, so nothing was saved: set a value that holds, or null "
            + "to keep the layer below.*");

        (await host.LoadSettingsAsync(Ct)).Version.Should().Be(0);
    }

    [Fact]
    public async Task Given_A_Target_That_Only_Another_Layers_Maximum_Decides_Saving_Stores_It()
    {
        // Arrange
        var host = Host<LimitsAgent>();
        await host.SaveSettingsAsync(new() { MaxInputTokens = 1_000 }, 0, Ct);

        // Act
        var version = await host
            .WithSettingsId(Data.Persistence.SettingsId)
            .SaveSettingsAsync(new() { TargetInputTokens = 2_000 }, 0, Ct);

        // Assert
        version.Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Given_A_Blank_Model_Saving_Throws_And_Stores_Nothing(string model)
    {
        // Arrange
        var host = Host<LimitsAgent>();

        // Act
        var save = () => host.SaveSettingsAsync(new() { Model = model }, 0, Ct);

        // Assert
        await save.Should().ThrowAsync<ArgumentException>().WithMessage($"*Model '{model}'*name a model*");
        (await host.LoadSettingsAsync(Ct)).Version.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Given_A_Blank_Provider_Saving_Throws_And_Stores_Nothing(string provider)
    {
        // Arrange
        var host = Host<LimitsAgent>();

        // Act
        var save = () => host.SaveSettingsAsync(new() { Provider = provider }, 0, Ct);

        // Assert
        await save.Should().ThrowAsync<ArgumentException>().WithMessage($"*Provider '{provider}'*name a provider*");
        (await host.LoadSettingsAsync(Ct)).Version.Should().Be(0);
    }

    [Fact]
    public async Task Given_A_Blank_Summary_Provider_Saving_Throws_Naming_It()
    {
        // Arrange
        var host = Host<LimitsAgent>();

        // Act
        var save = () => host.SaveSettingsAsync(new() { SummaryProvider = " " }, 0, Ct);

        // Assert
        await save.Should().ThrowAsync<ArgumentException>().WithMessage("*SummaryProvider ' '*name a provider*");
    }

    [Fact]
    public async Task Given_A_Change_To_A_Blank_Model_Updating_Throws_And_Stores_Nothing()
    {
        // Arrange
        var host = Host<LimitsAgent>();

        // Act
        var update = () => host.UpdateSettingsAsync(settings => settings with { Model = " " }, Ct);

        // Assert
        await update.Should().ThrowAsync<ArgumentException>().WithParameterName("settings");
        (await host.LoadSettingsAsync(Ct)).Version.Should().Be(0);
    }

    [Fact]
    public async Task Given_A_Function_That_Returns_Null_Updating_Throws_And_Saves_Nothing()
    {
        // Arrange
        var host = Host<ProbeAgent>();

        // Act
        var update = () => host.UpdateSettingsAsync(_ => null!, Ct);

        // Assert
        await update.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"*UpdateSettingsAsync*'{host.Agent.Name}'*returned no settings*");

        (await host.LoadSettingsAsync(Ct)).Version.Should().Be(0);
    }

    [Fact]
    public async Task Given_A_Store_Of_Agent_Settings_Alone_A_Run_Applies_Them()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift()
            .WithStore<AgentSettingsOnlyStore>()
            .WithScriptedProviders();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>();

        ChatClient.Reply(Text(Data.Tool.Answer));
        await host.UpdateSettingsAsync(
            settings => settings with { Provider = Data.Agent.Provider, Model = Data.Agent.Model },
            Ct);

        // Act
        await host.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(Data.Agent.Provider, Data.Agent.Model));
    }

    [Fact]
    public async Task Given_No_Store_An_Agents_Own_Settings_Are_Kept_In_Memory_And_A_Run_Applies_Them()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithScriptedProviders();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>();

        ChatClient.Reply(Text(Data.Tool.Answer));
        await host.UpdateSettingsAsync(settings => settings with { Model = Data.Agent.Model }, Ct);

        // Act
        await host.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(ProbeAgent.ModelProvider, Data.Agent.Model));
    }

    [Fact]
    public async Task Given_No_Store_The_First_Save_Of_An_Agents_Own_Settings_Warns_That_It_Keeps_Them_In_Memory()
    {
        // Arrange
        var logs = new WarningLog();
        var services = new ServiceCollection();
        services.AddTypeshift().WithScriptedProviders();
        services.AddSingleton<ILoggerFactory>(logs);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>();

        // Act
        await host.UpdateSettingsAsync(settings => settings with { Model = Data.Agent.Model }, Ct);
        await host.UpdateSettingsAsync(settings => settings with { Temperature = Data.Agent.Temperature }, Ct);

        // Assert
        logs.Warnings.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Store_Without_Agent_Settings_A_Run_Without_A_Settings_Id_Runs_From_The_Code()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<SettingsOnlyStore>().WithScriptedProviders();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>();

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await host.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(ProbeAgent.ModelProvider, ProbeAgent.Model));
    }

    [Fact]
    public void Given_A_Type_That_Is_No_Store_Registering_It_Fails_Naming_TSH028()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
#pragma warning disable TSH028 // the analyzer reports it at build time; this checks the registration's own guard
        var register = () => services.AddTypeshift().WithStore<ProvidedModels>();
#pragma warning restore TSH028

        // Assert
        register.Should().Throw<InvalidOperationException>().WithMessage("*TSH028*");
    }

    [Fact]
    public async Task Given_A_Store_Without_Agent_Settings_Updating_Them_Fails_Naming_IAgentSettingsStore()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<SettingsOnlyStore>().WithScriptedProviders();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>();

        // Act
        var update = () => host.UpdateSettingsAsync(settings => settings with { Model = Data.Agent.Model }, Ct);

        // Assert
        await update.Should().ThrowAsync<InvalidOperationException>().WithMessage("*IAgentSettingsStore*");
    }

    [Fact]
    public async Task Given_A_Store_Without_Agent_Settings_The_Message_Names_The_File_Store()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<SettingsOnlyStore>().WithScriptedProviders();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>();

        // Act
        var update = () => host.UpdateSettingsAsync(settings => settings with { Model = Data.Agent.Model }, Ct);

        // Assert
        await update.Should().ThrowAsync<InvalidOperationException>().WithMessage(
            "No IAgentSettingsStore is registered, so nothing keeps an agent's own settings: call WithFileStore() "
            + "after AddTypeshift() to keep them in files, or WithStore<T>() with a store of yours that implements "
            + "it. The stores your app registered keep ISettingsStore<Guid> only, and beside a registered store "
            + "Typeshift keeps nothing in memory.");
    }
}
