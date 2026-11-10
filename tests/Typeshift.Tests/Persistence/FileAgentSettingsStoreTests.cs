using System.Text.Json;
using Typeshift.Tests.Agents.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Persistence;

public sealed class FileAgentSettingsStoreTests : AgentTestBase
{
    // A folder of this test's own, so tests that run at once never share a file
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"typeshift-{Guid.NewGuid():N}");

    [Fact]
    public async Task Given_A_Folder_The_Store_Passes_The_Check()
    {
        // Arrange
        var store = new FileAgentSettingsStore(_folder);

        // Act
        var verify = () => store.VerifyAgentSettingsAsync(Data.Persistence.Kind, Ct);

        // Assert
        await verify.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_Settings_Saved_By_One_Store_A_Run_With_Another_Over_The_Folder_Applies_Them()
    {
        // Arrange
        await using (var first = Provider(new FileAgentSettingsStore(_folder)))
        {
            await using var scope = first.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>().ConfigureAsync(
                settings => settings with { Temperature = Data.Agent.Temperature },
                Ct);
        }

        await using var second = Provider(new FileAgentSettingsStore(_folder));
        await using var run = second.CreateAsyncScope();

        // Act
        var options = await run.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>().ResolveOptionsAsync(ct: Ct);

        // Assert
        options.Temperature.Should().Be(Data.Agent.Temperature);
    }

    [Fact]
    public async Task Given_A_Save_The_File_Holds_The_Version_And_The_Settings_As_Json()
    {
        // Arrange
        var store = new FileAgentSettingsStore(_folder);

        // Act
        await store.SaveAsync(Data.Persistence.Kind, """{"temperature":0.4}""", 0, Ct);

        // Assert
        var path = Path.Combine(_folder, $"{Data.Persistence.Kind}.json");
        using var file = JsonDocument.Parse(await File.ReadAllTextAsync(path, Ct));
        file.RootElement.GetProperty("version").GetInt64().Should().Be(1);
        file.RootElement.GetProperty("settings").GetProperty("temperature").GetDouble().Should().Be(0.4);
    }

    [Fact]
    public async Task Given_A_Kind_That_Is_No_File_Name_Saving_Fails_Naming_WithStorageKind()
    {
        // Arrange
        var store = new FileAgentSettingsStore(_folder);

        // Act
        var save = () => store.SaveAsync("../outside", "{}", 0, Ct);

        // Assert
        await save.Should().ThrowAsync<ArgumentException>().WithMessage("*WithStorageKind*");
    }

    protected override ValueTask DisposeCoreAsync()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        return ValueTask.CompletedTask;
    }

    private ServiceProvider Provider(FileAgentSettingsStore store)
    {
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore(store).WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        return services.BuildServiceProvider(validateScopes: true);
    }
}
