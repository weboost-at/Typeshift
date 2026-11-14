using System.Text.Json;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Persistence.Support;
using Typeshift.Tests.Testing.Fakes;
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
    public async Task Given_A_Kind_With_Letters_Outside_Ascii_The_File_Holds_It_As_Written()
    {
        // Arrange
        var store = new FileAgentSettingsStore(_folder);

        // Act
        await store.SaveAsync("ÜbersetzerAgent", "{}", 0, Ct);

        // Assert
        (await File.ReadAllTextAsync(OnlyFile(), Ct)).Should().Contain("\"kind\": \"ÜbersetzerAgent\"");
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
    public async Task Given_A_Kind_Of_Letters_Digits_Underscores_And_Hyphens_Its_File_Is_Named_After_It()
    {
        // Arrange
        var store = new FileAgentSettingsStore(_folder);

        // Act
        await store.SaveAsync("Support-Agent_2", "{}", 0, Ct);

        // Assert
        Path.GetFileName(OnlyFile()).Should().Be("Support-Agent_2.json");
    }

    [Theory]
    [InlineData("a/b", "a%2Fb.json")]
    [InlineData("NUL", "%4EUL.json")]
    public async Task Given_A_Kind_With_Other_Characters_Its_File_Name_Escapes_Their_Bytes(string kind, string name)
    {
        // Arrange
        var store = new FileAgentSettingsStore(_folder);

        // Act
        await store.SaveAsync(kind, "{}", 0, Ct);

        // Assert
        Path.GetFileName(OnlyFile()).Should().Be(name);
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
    public async Task Given_A_File_That_Is_No_Json_Loading_Fails_Naming_The_File_And_Its_Shape()
    {
        // Arrange
        var store = new FileAgentSettingsStore(_folder);
        await store.SaveAsync(Data.Persistence.Kind, "{}", 0, Ct);
        await File.WriteAllTextAsync(OnlyFile(), """{ "version": 1, }""", Ct);

        // Act
        var load = () => store.LoadAsync(Data.Persistence.Kind, Ct);

        // Assert
        await load.Should().ThrowAsync<InvalidDataException>().WithMessage($"{OnlyFile()} is no JSON*\"version\": n*");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("5")]
    [InlineData("null")]
    [InlineData("\"text\"")]
    [InlineData("{\"kind\":5,\"version\":1,\"settings\":{}}")]
    [InlineData("{\"kind\":null,\"version\":1,\"settings\":{}}")]
    [InlineData("{\"version\":\"3\",\"settings\":{}}")]
    [InlineData("{\"version\":1,\"settings\":[]}")]
    public async Task Given_A_File_Of_Another_Shape_Loading_Fails_Naming_The_File_And_The_Shape(string content)
    {
        // Arrange
        var store = new FileAgentSettingsStore(_folder);
        await store.SaveAsync(Data.Persistence.Kind, "{}", 0, Ct);
        await File.WriteAllTextAsync(OnlyFile(), content, Ct);

        // Act
        var load = () => store.LoadAsync(Data.Persistence.Kind, Ct);

        // Assert
        await load.Should().ThrowAsync<InvalidDataException>()
            .WithMessage($"{OnlyFile()} is no settings file*\"version\": n*");
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
    public async Task Given_Two_Kinds_That_Differ_By_Case_Each_Reads_Its_Own_Or_Fails_Naming_WithStorageKind()
    {
        // Arrange
        var store = new FileAgentSettingsStore(_folder);
        string[] kinds = [Data.Persistence.Kind.ToLowerInvariant(), Data.Persistence.Kind.ToUpperInvariant()];
        string[] settings = ["""{"temperature":0.4}""", """{"temperature":0.9}"""];

        Directory.CreateDirectory(_folder);
        var ignoresCase = Directory.Exists(_folder.ToUpperInvariant());

        // Act
        var stored = async () =>
        {
            await store.SaveAsync(kinds[0], settings[0], 0, Ct);
            await store.SaveAsync(kinds[1], settings[1], 0, Ct);

            return new[] { await store.LoadAsync(kinds[0], Ct), await store.LoadAsync(kinds[1], Ct) };
        };

        // Assert
        if (ignoresCase) await stored.Should().ThrowAsync<InvalidDataException>().WithMessage("*WithStorageKind*");
        else (await stored()).Select(document => document!.Json).Should().Equal(settings);
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

    [Fact]
    public async Task Given_Settings_Saved_Through_WithFileStore_A_Provider_Over_The_Same_Folder_Applies_Them()
    {
        // Arrange
        await using (var first = Provider(typeshift => typeshift.WithFileStore(store => store.Folder = _folder)))
        {
            await using var scope = first.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>().ConfigureAsync(
                settings => settings with { Temperature = Data.Agent.Temperature },
                Ct);
        }

        await using var second = Provider(typeshift => typeshift.WithFileStore(store => store.Folder = _folder));
        await using var run = second.CreateAsyncScope();

        // Act
        var applied = await run.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>().ResolveOptionsAsync(ct: Ct);

        // Assert
        applied.Temperature.Should().Be(Data.Agent.Temperature);
    }

    [Fact]
    public void Given_No_Folder_The_File_Store_Lies_Under_The_Apps_Base_Directory()
    {
        // Act
        var options = new FileStoreOptions();

        // Assert
        options.Folder.Should().Be(Path.Combine(AppContext.BaseDirectory, "typeshift"));
    }

    [Fact]
    public async Task Given_A_Save_The_File_Lies_In_The_Settings_Folder_Of_The_Store()
    {
        // Arrange
        await using var provider = Provider(typeshift => typeshift.WithFileStore(store => store.Folder = _folder));

        // Act
        await provider.GetRequiredService<IAgentSettingsStore>().SaveAsync(Data.Persistence.Kind, "{}", 0, Ct);

        // Assert
        Path.GetDirectoryName(OnlyFile()).Should().Be(Path.Combine(_folder, "settings"));
    }

    [Fact]
    public void Given_A_Blank_Folder_WithFileStore_Fails_Naming_The_Option()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var register = () => services.AddTypeshift().WithFileStore(store => store.Folder = " ");

        // Assert
        register.Should().Throw<ArgumentException>().WithParameterName("FileStoreOptions.Folder");
    }

    [Fact]
    public async Task Given_A_Kind_Locked_By_Another_Save_The_File_Store_Waits_On_The_Apps_Clock()
    {
        // Arrange
        await using var provider = Provider(
            typeshift => typeshift.WithFileStore(store => store.Folder = _folder),
            new RefusingClock());

        var fileStore = provider.GetRequiredService<IAgentSettingsStore>();
        await fileStore.SaveAsync(Data.Persistence.Kind, "{}", 0, Ct);

        var otherSave = new FileStream(
            path: $"{OnlyFile()}.lock",
            mode: FileMode.Open,
            access: FileAccess.ReadWrite,
            share: FileShare.None);

        // Act
        var save = fileStore.SaveAsync(Data.Persistence.Kind, "{}", 1, Ct);
        await otherSave.DisposeAsync();

        // Assert
        await FluentActions.Awaiting(() => save).Should().ThrowAsync<NotSupportedException>()
            .WithMessage(RefusingClock.Refusal);
    }

    [Fact]
    public async Task Given_A_Lock_File_Held_Past_The_Bound_The_Save_Fails_Naming_It()
    {
        // Arrange
        var store = new FileAgentSettingsStore(_folder, Time);
        await store.SaveAsync(Data.Persistence.Kind, "{}", 0, Ct);

        await using var held = Hold($"{OnlyFile()}.lock");
        var save = store.SaveAsync(Data.Persistence.Kind, "{}", 1, Ct);

        // Act
        Time.Advance(TimeSpan.FromSeconds(30));

        // Assert
        await FluentActions.Awaiting(() => save).Should().ThrowAsync<TimeoutException>()
            .WithMessage($"{OnlyFile()}.lock has been held*30 seconds*");
    }

    [Fact]
    public async Task Given_A_Lock_File_Let_Go_Within_The_Bound_The_Save_Goes_Through()
    {
        // Arrange
        var store = new FileAgentSettingsStore(_folder, Time);
        await store.SaveAsync(Data.Persistence.Kind, "{}", 0, Ct);

        var held = Hold($"{OnlyFile()}.lock");
        var save = store.SaveAsync(Data.Persistence.Kind, "{}", 1, Ct);

        // Act
        await held.DisposeAsync();
        Time.Advance(TimeSpan.FromSeconds(29));

        // Assert
        (await save).Should().Be(2);
    }

    [Fact]
    public async Task Given_A_Kind_Locked_By_Another_Save_The_Store_Logs_Which_Lock_File_It_Waits_For()
    {
        // Arrange
        var log = new DebugLog();
        var store = new FileAgentSettingsStore(_folder, Time, log);
        await store.SaveAsync(Data.Persistence.Kind, "{}", 0, Ct);
        var held = Hold($"{OnlyFile()}.lock");

        // Act
        var save = store.SaveAsync(Data.Persistence.Kind, "{}", 1, Ct);
        await held.DisposeAsync();
        Time.Advance(TimeSpan.FromSeconds(1));
        await save;

        // Assert
        log.Messages.Should().ContainSingle(message => message.Contains($"{OnlyFile()}.lock"));
    }

    [Fact]
    public async Task Given_Two_Saves_The_Store_Logs_The_Folder_It_Keeps_The_Settings_In_Once()
    {
        // Arrange
        var log = new EventLog();
        var store = new FileAgentSettingsStore(_folder, Time, log.CreateLogger("Typeshift"));

        // Act
        await store.SaveAsync(Data.Persistence.Kind, "{}", 0, Ct);
        await store.SaveAsync(Data.Persistence.Kind, "{}", 1, Ct);

        // Assert
        log.Of("FileStoreFolder").Select(logged => logged.Message)
            .Should()
            .Equal($"The file store keeps agents' own settings in {_folder}.");
    }

    [Fact]
    public async Task Given_An_Unfinished_File_Of_A_Save_That_Died_The_Next_Save_Of_Its_Kind_Removes_It()
    {
        // Arrange
        var store = new FileAgentSettingsStore(_folder);
        await store.SaveAsync(Data.Persistence.Kind, "{}", 0, Ct);
        await File.WriteAllTextAsync($"{OnlyFile()}.{Data.Persistence.OtherId:N}.saving", "half a file", Ct);

        // Act
        await store.SaveAsync(Data.Persistence.Kind, "{}", 1, Ct);

        // Assert
        Directory.GetFiles(_folder, "*.saving").Should().BeEmpty();
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

    private string OnlyFile() => Directory.GetFiles(_folder, "*.json", SearchOption.AllDirectories).Single();

    // The kind's lock file, held as a save of another store or process holds it
    private static FileStream Hold(string lockFile) =>
        new(path: lockFile, mode: FileMode.Open, access: FileAccess.ReadWrite, share: FileShare.None);

    private ServiceProvider Provider(IAgentSettingsStore store) => Provider(typeshift => typeshift.WithStore(store));

    private ServiceProvider Provider(Action<TypeshiftBuilder> withStore, TimeProvider? time = null)
    {
        var services = new ServiceCollection();
        withStore(services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>());
        services.AddSingleton(ChatClient).AddSingleton(Models).AddSingleton(time ?? Time);

        return services.BuildServiceProvider(validateScopes: true);
    }
}
