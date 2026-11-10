using System.Text.Json;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Persistence.Support;
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
    public async Task Given_A_Save_The_File_Holds_The_Kind_The_Version_And_The_Settings_As_Json()
    {
        // Arrange
        var store = new FileAgentSettingsStore(_folder);

        // Act
        await store.SaveAsync(Data.Persistence.Kind, """{"temperature":0.4}""", 0, Ct);

        // Assert
        using var file = JsonDocument.Parse(await File.ReadAllTextAsync(OnlyFile(), Ct));
        file.RootElement.GetProperty("kind").GetString().Should().Be(Data.Persistence.Kind);
        file.RootElement.GetProperty("version").GetInt64().Should().Be(1);
        file.RootElement.GetProperty("settings").GetProperty("temperature").GetDouble().Should().Be(0.4);
    }

    [Fact]
    public async Task Given_Two_Stores_Over_One_Folder_Saving_At_Once_Every_Save_Gets_A_Version_Of_Its_Own()
    {
        // Arrange
        FileAgentSettingsStore[] stores = [new(_folder), new(_folder)];

        // Act
        var versions = await Task.WhenAll(Enumerable.Range(0, 40).Select(save => SaveUntilWonAsync(stores[save % 2])));

        // Assert
        versions.Should().OnlyHaveUniqueItems();
        (await stores[0].LoadAsync(Data.Persistence.Kind, Ct))!.Version.Should().Be(40);
    }

    [Fact]
    public async Task Given_Kinds_Of_Any_Characters_Each_Keeps_A_File_Of_Its_Own_In_The_Folder()
    {
        // Arrange
        var store = new FileAgentSettingsStore(_folder);
        string[] kinds = ["ÜbersetzerAgent", "../outside", "a/b", "a%2Fb", "NUL", "research agent"];

        // Act
        foreach (var kind in kinds) await store.SaveAsync(kind, "{}", 0, Ct);

        // Assert
        Directory.GetFiles(_folder, "*.json").Should().HaveCount(kinds.Length);
        Directory.GetParent(_folder)!.GetFiles("outside*").Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Save_No_Unfinished_File_Is_Left_In_The_Folder()
    {
        // Arrange
        var store = new FileAgentSettingsStore(_folder);

        // Act
        await store.SaveAsync(Data.Persistence.Kind, "{}", 0, Ct);

        // Assert
        Directory.GetFiles(_folder, "*.saving").Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_File_Edited_Into_Something_Else_Loading_Fails_Naming_The_File_And_Its_Shape()
    {
        // Arrange
        var store = new FileAgentSettingsStore(_folder);
        await store.SaveAsync(Data.Persistence.Kind, "{}", 0, Ct);
        await File.WriteAllTextAsync(OnlyFile(), """{ "settings": {} }""", Ct);

        // Act
        var load = () => store.LoadAsync(Data.Persistence.Kind, Ct);

        // Assert
        await load.Should().ThrowAsync<InvalidDataException>().WithMessage($"{OnlyFile()}*\"version\": n*");
    }

    [Fact]
    public async Task Given_A_File_Of_Another_Kind_Loading_Fails_Naming_WithStorageKind()
    {
        // Arrange
        var store = new FileAgentSettingsStore(_folder);
        await store.SaveAsync(Data.Persistence.Kind, "{}", 0, Ct);
        var file = await File.ReadAllTextAsync(OnlyFile(), Ct);
        await File.WriteAllTextAsync(OnlyFile(), file.Replace(Data.Persistence.Kind, "Other"), Ct);

        // Act
        var load = () => store.LoadAsync(Data.Persistence.Kind, Ct);

        // Assert
        await load.Should().ThrowAsync<InvalidDataException>().WithMessage("*WithStorageKind*");
    }

    [Fact]
    public async Task Given_A_Store_Held_As_Its_Interface_It_Serves_Runs_And_The_Container_Leaves_It_Undisposed()
    {
        // Arrange
        var store = new DisposalCountingStore(new FileAgentSettingsStore(_folder));

        await using (var provider = Provider(store))
        {
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>().ConfigureAsync(
                settings => settings with { Temperature = Data.Agent.Temperature },
                Ct);
        }

        // Act
        var stored = await store.LoadAsync("ProbeAgent", Ct);

        // Assert
        stored!.Version.Should().Be(1);
        store.Disposed.Should().Be(0);
    }

    protected override ValueTask DisposeCoreAsync()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        return ValueTask.CompletedTask;
    }

    // A save as ConfigureAsync makes it: load, then save with the loaded version, again after each lost save
    private async Task<long> SaveUntilWonAsync(FileAgentSettingsStore store)
    {
        while (true)
        {
            var loaded = await store.LoadAsync(Data.Persistence.Kind, Ct);

            try
            {
                return await store.SaveAsync(Data.Persistence.Kind, "{}", loaded?.Version ?? 0, Ct);
            }
            catch (StoreConflictException)
            {
                Ct.ThrowIfCancellationRequested();
            }
        }
    }

    private string OnlyFile() => Directory.GetFiles(_folder, "*.json").Single();

    private ServiceProvider Provider(IAgentSettingsStore store)
    {
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore(store).WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        return services.BuildServiceProvider(validateScopes: true);
    }
}
