using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Hosting.Support;
using Typeshift.Tests.Persistence.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Registration;

// D38: what the chain's store calls register, and that a store interface has one store
public class StoreRegistrationTests : AgentTestBase
{
    // A folder of this test's own, so tests that run at once never share a file
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"typeshift-{Guid.NewGuid():N}");

    [Fact]
    public async Task Given_A_Store_Built_By_A_Factory_Each_Of_Its_Interfaces_Resolves_The_One_Store()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore(_ => new InMemoryTypeshiftStore<int>());
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var conversations = provider.GetRequiredService<IConversationStore<int>>();

        // Assert
        conversations.Should().BeSameAs(provider.GetRequiredService<ISettingsStore<int>>());
    }

    [Fact]
    public async Task Given_A_Factory_That_Returns_A_Store_Interface_The_Store_Is_Registered_As_That_Interface()
    {
        // Arrange
        var store = new InMemoryTypeshiftStore<int>();
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<ISettingsStore<int>>(_ => store);
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var settings = provider.GetRequiredService<ISettingsStore<int>>();

        // Assert
        settings.Should().BeSameAs(store);
    }

    [Fact]
    public async Task Given_A_Factory_That_Returns_A_Store_Interface_The_Stores_Other_Interfaces_Stay_Unregistered()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<ISettingsStore<int>>(_ => new InMemoryTypeshiftStore<int>());
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var conversations = provider.GetService<IConversationStore<int>>();

        // Assert
        conversations.Should().BeNull();
    }

    [Fact]
    public async Task Given_A_Store_Built_By_A_Factory_The_Container_Disposes_It()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore(_ => new DisposableStore());
        var provider = services.BuildServiceProvider(validateScopes: true);
        var store = provider.GetRequiredService<ISettingsStore<Guid>>();

        // Act
        await provider.DisposeAsync();

        // Assert
        ((DisposableStore)store).Disposed.Should().BeTrue();
    }

    [Fact]
    public void Given_No_Factory_Registering_A_Store_Throws()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var register = () => services.AddTypeshift().WithStore<InMemoryTypeshiftStore<int>>(create: null!);

        // Assert
        register.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Given_Two_Stores_Of_One_Interface_The_Second_Call_Throws_Naming_Both_And_The_Interface()
    {
        // Arrange
        var typeshift = new ServiceCollection().AddTypeshift().WithStore<SettingsOnlyStore>();

        // Act
        var register = () => typeshift.WithStore<DisposableStore>();

        // Assert
        register.Should().Throw<InvalidOperationException>().WithMessage(
            "DisposableStore and SettingsOnlyStore are both registered as ISettingsStore<Guid>, and a store interface "
            + "has one store: remove one of the two calls, or pass replace: true to the later one, as in "
            + "WithStore<T>(replace: true), when its store replaces the other, as a test host's does.");
    }

    [Fact]
    public void Given_A_Store_Of_All_Three_And_The_Memory_Store_Of_Its_Id_Type_The_Message_Names_Each_Interface()
    {
        // Arrange
        var typeshift = new ServiceCollection().AddTypeshift().WithStore(new NewestFirstStore());

        // Act
        var register = () => typeshift.WithStore<InMemoryTypeshiftStore<Guid>>();

        // Assert
        register.Should().Throw<InvalidOperationException>().WithMessage(
            "InMemoryTypeshiftStore<Guid> and NewestFirstStore are both registered as IConversationStore<Guid>, "
            + "IDecisionStore<Guid> and ISettingsStore<Guid>, and a store interface has one store*");
    }

    [Fact]
    public void Given_Two_Instances_Of_One_Store_Type_The_Second_Call_Throws_Naming_Another_Of_It()
    {
        // Arrange
        var typeshift = new ServiceCollection().AddTypeshift().WithStore(new SettingsOnlyStore());

        // Act
        var register = () => typeshift.WithStore(new SettingsOnlyStore());

        // Assert
        register.Should().Throw<InvalidOperationException>().WithMessage(
            "SettingsOnlyStore and another SettingsOnlyStore are both registered as ISettingsStore<Guid>*");
    }

    [Fact]
    public void Given_Two_Chains_Of_One_Service_Collection_A_Second_Store_Of_An_Interface_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<SettingsOnlyStore>();

        // Act
        var register = () => services.AddTypeshift().WithStore<DisposableStore>();

        // Assert
        register.Should().Throw<InvalidOperationException>().WithMessage("*ISettingsStore<Guid>*");
    }

    [Fact]
    public void Given_A_Store_The_Chain_Refused_Nothing_Of_It_Is_Registered()
    {
        // Arrange
        var services = new ServiceCollection();
        var typeshift = services.AddTypeshift().WithStore<SettingsOnlyStore>();
        var registered = services.Count;

        // Act
        Record.Exception(() => typeshift.WithStore<DisposableStore>());

        // Assert
        services.Should().HaveCount(registered);
    }

    [Fact]
    public async Task Given_A_Store_That_Replaces_It_Alone_Serves_The_Interface()
    {
        // Arrange
        var replacement = new DisposableStore();
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<SettingsOnlyStore>().WithStore(replacement, replace: true);
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var settings = provider.GetServices<ISettingsStore<Guid>>();

        // Assert
        settings.Should().Equal(replacement);
    }

    [Fact]
    public async Task Given_A_Store_That_Replaces_The_Interfaces_It_Does_Not_Implement_Stay_With_The_First()
    {
        // Arrange
        var first = new InMemoryTypeshiftStore<Guid>();
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore(first).WithStore<SettingsOnlyStore>(replace: true);
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var conversations = provider.GetRequiredService<IConversationStore<Guid>>();

        // Assert
        conversations.Should().BeSameAs(first);
    }

    [Fact]
    public async Task Given_The_Same_Store_Registered_Twice_The_Second_Call_Registers_Nothing()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<SettingsOnlyStore>().WithStore<SettingsOnlyStore>();
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var settings = provider.GetServices<ISettingsStore<Guid>>();

        // Assert
        settings.Should().ContainSingle();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_The_File_Store_And_The_Memory_Store_In_Either_Order_Agent_Settings_Go_To_The_Files(
        bool fileStoreFirst)
    {
        // Arrange
        var services = new ServiceCollection();
        var typeshift = services.AddTypeshift();

        if (fileStoreFirst) typeshift.WithFileStore(store => store.Folder = _folder);

        typeshift.WithStore<InMemoryTypeshiftStore<Guid>>();

        if (!fileStoreFirst) typeshift.WithFileStore(store => store.Folder = _folder);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();

        // Act
        await scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .ConfigureAsync(settings => settings with { Temperature = Data.Agent.Temperature }, Ct);

        // Assert
        Directory.GetFiles(_folder, "*.json", SearchOption.AllDirectories).Should().ContainSingle();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_A_Store_Of_Agent_Settings_And_The_Memory_Store_In_Either_Order_The_Store_Keeps_Them(
        bool memoryFirst)
    {
        // Arrange
        var store = new AgentSettingsOnlyStore();
        var services = new ServiceCollection();
        var typeshift = services.AddTypeshift();

        if (memoryFirst) typeshift.WithStore<InMemoryTypeshiftStore<Guid>>();

        typeshift.WithStore(store);

        if (!memoryFirst) typeshift.WithStore<InMemoryTypeshiftStore<Guid>>();

        await using var provider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var agentSettings = provider.GetServices<IAgentSettingsStore>();

        // Assert
        agentSettings.Should().Equal(store);
    }

    [Fact]
    public async Task Given_An_Agent_Settings_Store_The_App_Registered_Itself_The_Memory_Store_Leaves_Them_To_It()
    {
        // Arrange
        var store = new AgentSettingsOnlyStore();
        var services = new ServiceCollection();
        services.AddSingleton<IAgentSettingsStore>(store);
        services.AddTypeshift().WithStore<InMemoryTypeshiftStore<Guid>>();
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var agentSettings = provider.GetServices<IAgentSettingsStore>();

        // Assert
        agentSettings.Should().Equal(store);
    }

    [Fact]
    public async Task Given_The_Memory_Store_Alone_It_Keeps_Agents_Own_Settings()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<InMemoryTypeshiftStore<Guid>>();
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var agentSettings = provider.GetRequiredService<IAgentSettingsStore>();

        // Assert
        agentSettings.Should().BeSameAs(provider.GetRequiredService<InMemoryTypeshiftStore<Guid>>());
    }

    [Fact]
    public async Task Given_Memory_Stores_Of_Two_Id_Types_The_First_Keeps_Agents_Own_Settings()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithStore<InMemoryTypeshiftStore<Guid>>().WithStore<InMemoryTypeshiftStore<int>>();
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var agentSettings = provider.GetServices<IAgentSettingsStore>();

        // Assert
        agentSettings.Should().Equal(provider.GetRequiredService<InMemoryTypeshiftStore<Guid>>());
    }

    [Fact]
    public async Task Given_The_Memory_Store_Registered_To_Replace_It_Takes_Agents_Own_Settings_Over()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddTypeshift()
            .WithFileStore(store => store.Folder = _folder)
            .WithStore<InMemoryTypeshiftStore<Guid>>(replace: true);

        await using var provider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var agentSettings = provider.GetServices<IAgentSettingsStore>();

        // Assert
        agentSettings.Should().Equal(provider.GetRequiredService<InMemoryTypeshiftStore<Guid>>());
    }

    [Fact]
    public async Task Given_The_File_Store_Registered_Twice_For_One_Folder_The_Second_Call_Registers_Nothing()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddTypeshift()
            .WithFileStore(store => store.Folder = _folder)
            .WithFileStore(store => store.Folder = _folder);

        await using var provider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var agentSettings = provider.GetServices<IAgentSettingsStore>();

        // Assert
        agentSettings.Should().ContainSingle();
    }

    [Fact]
    public void Given_File_Stores_Of_Two_Folders_The_Second_Call_Throws_Naming_Both_Folders()
    {
        // Arrange
        var other = $"{_folder}-other";
        var typeshift = new ServiceCollection().AddTypeshift().WithFileStore(store => store.Folder = _folder);

        // Act
        var register = () => typeshift.WithFileStore(store => store.Folder = other);

        // Assert
        register.Should().Throw<InvalidOperationException>().WithMessage(
            $"WithFileStore(Folder = '{other}') and WithFileStore(Folder = '{_folder}') are both registered as "
            + "IAgentSettingsStore*");
    }

    protected override ValueTask DisposeCoreAsync()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);

        return ValueTask.CompletedTask;
    }
}
