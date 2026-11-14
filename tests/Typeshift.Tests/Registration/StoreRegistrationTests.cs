using Typeshift.Tests.Hosting.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Registration;

// D38: what the chain's store calls register, and for which store interfaces
public class StoreRegistrationTests : UnitTestBase
{
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
}
