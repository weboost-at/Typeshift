using Typeshift.Tests.Hosting.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Registration;

// D63, D88: the default provider and the connector are each one choice of the app's, so a second call that names
// another throws, as a second store for one interface does
public class ProviderRegistrationTests : UnitTestBase
{
    [Fact]
    public void Given_A_Second_Default_Provider_Of_Another_Key_The_Chain_Throws_Naming_Both()
    {
        // Arrange
        var typeshift = new ServiceCollection().AddTypeshift().WithDefaultProvider(Data.Agent.Provider);

        // Act
        var register = () => typeshift.WithDefaultProvider(Vendor.Other);

        // Assert
        register.Should().Throw<InvalidOperationException>().WithMessage(
            "WithDefaultProvider('Other') names a second default provider, and "
            + $"'{Data.Agent.Provider}' is the default provider already: the default serves every run that names no "
            + "provider, so name it once.");
    }

    [Fact]
    public void Given_The_Same_Default_Provider_Twice_The_Chain_Keeps_It()
    {
        // Arrange
        var typeshift = new ServiceCollection().AddTypeshift().WithDefaultProvider(Data.Agent.Provider);

        // Act
        var register = () => typeshift.WithDefaultProvider(Data.Agent.Provider);

        // Assert
        register.Should().NotThrow();
    }

    [Fact]
    public void Given_A_Second_Provider_Connector_Of_Another_Type_The_Chain_Throws_Naming_Both()
    {
        // Arrange
        var typeshift = new ServiceCollection().AddTypeshift().WithProviderConnector<CountingConnector>();

        // Act
        var register = () => typeshift.WithProviderConnector<FailingConnector>();

        // Assert
        register.Should().Throw<InvalidOperationException>().WithMessage(
            "WithProviderConnector<FailingConnector>() names a second provider connector, and CountingConnector is "
            + "registered as the IChatProviderConnector already: an app has one connector, which may switch on the "
            + "document's Kind. Register one of them.");
    }

    [Fact]
    public void Given_A_Connector_The_App_Registered_Itself_The_Chains_Other_Connector_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IChatProviderConnector, CountingConnector>();

        // Act
        var register = () => services.AddTypeshift().WithProviderConnector<FailingConnector>();

        // Assert
        register.Should().Throw<InvalidOperationException>().WithMessage("*and CountingConnector is registered*");
    }

    [Fact]
    public void Given_The_Same_Provider_Connector_Twice_It_Is_Registered_Once()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTypeshift().WithProviderConnector<CountingConnector>().WithProviderConnector<CountingConnector>();

        // Assert
        services.Where(service => service.ServiceType == typeof(IChatProviderConnector)).Should().ContainSingle();
    }
}
