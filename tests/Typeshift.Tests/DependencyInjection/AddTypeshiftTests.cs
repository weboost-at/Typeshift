using System.Text.Json.Serialization.Metadata;
using Typeshift.Agents;
using Typeshift.Json;
using Typeshift.Persistence.InMemory;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Toolsets.Companies;
using Typeshift.Tests.Toolsets.Support;
using Typeshift.Toolsets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Typeshift.Tests.DependencyInjection;

public class AddTypeshiftTests : HostTestBase
{
    [Fact]
    public void Registers_The_Toolsets_Of_The_Calling_Assembly()
    {
        // Act
        var toolsets = Services.GetServices<Toolset>();

        // Assert
        toolsets.Should().ContainSingle(toolset => toolset is ProbeToolset);
    }

    [Fact]
    public void Registers_The_Toolsets_Of_A_Referenced_Assembly()
    {
        // Act
        var toolsets = Services.GetServices<Toolset>();

        // Assert
        toolsets.Should().ContainSingle(toolset => toolset is CompanyToolset);
    }

    [Fact]
    public void Registers_The_Agents_Of_The_Calling_Assembly()
    {
        // Act
        var agents = Services.GetServices<AgentDefinition>();

        // Assert
        agents.Should().ContainSingle(agent => agent is ProbeAgent);
    }

    [Fact]
    public void Registers_The_Agents_Of_A_Referenced_Assembly()
    {
        // Act
        var agents = Services.GetServices<AgentDefinition>();

        // Assert
        agents.Should().ContainSingle(agent => agent is ResearchAgent);
    }

    [Fact]
    public void Registers_The_Json_Contexts_Of_The_Calling_Assembly()
    {
        // Act
        var typeInfo = Json.GetTypeInfo<ProbeResult>();

        // Assert
        typeInfo.OriginatingResolver.Should().BeSameAs(ProbeJsonContext.Default);
    }

    [Fact]
    public void Registers_The_Json_Contexts_Of_A_Referenced_Assembly()
    {
        // Act
        var typeInfo = Json.GetTypeInfo<FindCompaniesSettings>();

        // Assert
        typeInfo.OriginatingResolver!.GetType().Name.Should().Be("CompanyJsonContext");
    }

    [Fact]
    public async Task Given_A_Resolver_The_Host_Configures_It_Is_Asked_Before_The_Json_Contexts()
    {
        // Arrange
        var resolver = new DefaultJsonTypeInfoResolver();
        var services = new ServiceCollection().AddTypeshift();

        services.Configure<AgentJsonOptions>(options => options.TypeInfoResolverChain.Add(resolver));
        await using var provider = services.BuildServiceProvider();

        // Act
        var typeInfo = provider.GetRequiredService<AgentJson>().GetTypeInfo<ProbeResult>();

        // Assert
        typeInfo.OriginatingResolver.Should().BeSameAs(resolver);
    }

    [Fact]
    public async Task Given_A_Json_Context_The_Host_Adds_Itself_It_Is_Added_Once()
    {
        // Arrange
        var services = new ServiceCollection().AddTypeshift();

        services.Configure<AgentJsonOptions>(options => options.TypeInfoResolverChain.Add(ProbeJsonContext.Default));
        await using var provider = services.BuildServiceProvider();

        // Act
        var chain = provider.GetRequiredService<IOptions<AgentJsonOptions>>().Value.TypeInfoResolverChain;

        // Assert
        chain.Should().ContainSingle(resolver => resolver == ProbeJsonContext.Default);
    }

    [Fact]
    public void Registers_A_Runtime_For_Every_Agent()
    {
        // Act
        var names = Services.GetServices<AgentRuntime>().Select(runtime => runtime.Definition.Name);

        // Assert
        names.Should().BeEquivalentTo(
            "probe_agent",
            "hosted_tool_agent",
            nameof(UnnamedAgent),
            nameof(ExcludingAgent),
            "research_agent");
    }

    [Fact]
    public void Given_ValidateOnBuild_And_ValidateScopes_The_Container_Builds()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddTypeshift()
            .AddTypeshiftStore<InMemoryTypeshiftStore<Guid>, Guid>();

        var options = new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        };

        // Act & Assert
        services.Invoking(registered => registered.BuildServiceProvider(options)).Should().NotThrow();
    }

    [Fact]
    public void Given_A_Second_Call_Nothing_Is_Registered_Twice()
    {
        // Arrange
        var services = new ServiceCollection().AddTypeshift();
        var registered = services.Count;

        // Act
        services.AddTypeshift();

        // Assert
        services.Should().HaveCount(registered);
    }

    [Fact]
    public void Given_A_Call_That_Is_Not_Intercepted_It_Throws_What_To_Do()
    {
        // Arrange
        Func<IServiceCollection, IServiceCollection> addTypeshift = TypeshiftServiceCollectionExtensions.AddTypeshift;

        // Act
        var register = () => addTypeshift(new ServiceCollection());

        // Assert
        register.Should().Throw<InvalidOperationException>().WithMessage("*reference the Typeshift package*");
    }
}
