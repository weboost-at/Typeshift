using Typeshift.Generated;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Tools.Support;
using Typeshift.Tests.Toolsets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Registration;

public class AddTypeshiftTests : AgentTestBase
{
    [Fact]
    public void Given_AddTypeshift_Every_Agent_Has_A_Host_Over_One_Shared_Agent()
    {
        // Act
        var first = Host<ProbeAgent>();
        var second = Host<ProbeAgent>();

        // Assert
        first.Should().NotBeSameAs(second);
        first.Agent.Should().BeSameAs(second.Agent);
    }

    [Fact]
    public void Given_AddTypeshift_The_Toolsets_Of_Referenced_Assemblies_Are_Registered()
    {
        // Act
        var toolset = Services.GetRequiredService<CompanyToolset>();

        // Assert
        toolset.DisplayName.Should().Be("Companies");
    }

    [Fact]
    public void Given_A_Json_Context_In_A_Toolsets_Assembly_Tools_Ask_It_Before_The_Defaults()
    {
        // Act
        var contract = Services.GetRequiredService<ToolJson>().GetTypeInfo<IReadOnlyList<FoundCompany>>();

        // Assert
        contract.OriginatingResolver.Should().BeOfType<CompanyJsonContext>();
    }

    [Fact]
    public void Given_Xml_Docs_On_A_Parameters_Type_Its_Schema_Describes_Its_Properties()
    {
        // Act
        var search = SchemaToolset.CreateSearchCompanies(Services.GetRequiredService<ToolJson>());

        // Assert
        search.JsonSchema.Assert.ItemPropertyDescription("known", "country", "Two-letter ISO country code.");
    }

    [Fact]
    public void Given_AddTypeshift_Twice_Each_Module_Registers_Once()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTypeshift();
        services.AddTypeshift();

        // Assert
        services.Where(service => service.ServiceType == typeof(ProbeAgent)).Should().ContainSingle();
    }

    [Fact]
    public void Given_A_Call_The_Generator_Did_Not_Intercept_It_Fails_Saying_What_To_Check()
    {
        // Arrange
        Func<IServiceCollection, TypeshiftBuilder> add = TypeshiftServiceCollectionExtensions.AddTypeshift;

        // Act
        var register = () => add(new ServiceCollection());

        // Assert
        register.Should().Throw<InvalidOperationException>().WithMessage("*not intercepted*");
    }

    [Fact]
    public async Task Given_No_Chat_Client_Provider_A_Run_Fails_Naming_The_Registration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        // Act
        var run = () => scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>().RunAsync("", ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WithChatClientProvider*");
    }
}
