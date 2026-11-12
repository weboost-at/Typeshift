using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Hosting.Support;
using Typeshift.Tests.Tools.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Hosting;

// D75, D76: the runtime TypeshiftRuntime.Create builds, and the one AddTypeshift() registers
public class TypeshiftRuntimeTests : AgentTestBase
{
    private readonly CreatedMiddleware _created = [];

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddTypeshift()
            .WithRunMiddleware<DisposableMiddleware>();

        services.AddSingleton(_created);
    }

    [Fact]
    public async Task Given_A_Runtime_Over_A_Client_An_Agent_Runs_On_It()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        await using var runtime = TypeshiftRuntime.Create(ChatClient);
        await using var probe = runtime.Agent<ProbeAgent>();

        // Act
        var response = await probe.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Runtime_With_A_Store_An_Agents_Configured_Settings_Apply_To_Its_Runs()
    {
        // Arrange
        await using var runtime = TypeshiftRuntime.Create(
            ChatClient,
            typeshift => typeshift.WithStore<InMemoryTypeshiftStore>());

        await using var probe = runtime.Agent<ProbeAgent>();
        await using var configuring = runtime.Agent<ProbeAgent>();

        await configuring.ConfigureAsync(
            Data.Persistence.SettingsId,
            settings => settings with { Temperature = Data.Agent.Temperature },
            Ct);

        // Act
        var options = await probe.ResolveOptionsAsync(Data.Persistence.SettingsId, ct: Ct);

        // Assert
        options.Temperature.Should().Be(Data.Agent.Temperature);
    }

    [Fact]
    public async Task Given_A_Runtime_Disposed_After_A_Run_The_Client_It_Was_Given_Stays_Undisposed()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        var runtime = TypeshiftRuntime.Create(ChatClient);
        await using var probe = runtime.Agent<ProbeAgent>();
        await probe.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await runtime.DisposeAsync();

        // Assert
        ChatClient.Disposed.Should().BeFalse();
    }

    [Fact]
    public async Task Given_A_Runtime_Disposed_After_A_Run_Its_Containers_Singletons_Are_Disposed()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        var runtime = TypeshiftRuntime.Create(
            ChatClient,
            typeshift => typeshift.WithStore<DisposableStore>());

        await using var probe = runtime.Agent<ProbeAgent>();
        var store = runtime.Services.GetRequiredService<DisposableStore>();
        await probe.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        runtime.Dispose();

        // Assert
        store.Disposed.Should().BeTrue();
    }

    [Fact]
    public void Given_No_Client_Creating_A_Runtime_Throws()
    {
        // Act
        var create = () => TypeshiftRuntime.Create(null!);

        // Assert
        create.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Given_A_Container_The_Root_And_A_Scope_Resolve_One_Runtime()
    {
        // Act
        var root = Services.GetRequiredService<TypeshiftRuntime>();
        var scoped = Scope.GetRequiredService<TypeshiftRuntime>();

        // Assert
        scoped.Should().BeSameAs(root);
    }

    [Fact]
    public async Task Given_A_Runtime_From_The_Container_An_Agent_Runs_On_Its_Host()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        await using var probe = Runtime.Agent<ProbeAgent>();

        // Act
        var response = await probe.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_Two_Hosts_Of_The_Runtime_Their_Runs_Resolve_From_Scopes_Of_Their_Own()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Call(Data.Tool.SecondCallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        await using var first = Runtime.Agent<ProbeAgent>();
        await using var second = Runtime.Agent<ProbeAgent>();

        // Act
        var firstRun = await first.RunAsync(Data.Tool.Question, ct: Ct);
        var secondRun = await second.RunAsync(Data.Tool.Question, ct: Ct);
        var firstScope = firstRun.Assert.ToolResult<ProbeResult>(Data.Tool.CallId, Json).Scope;
        var secondScope = secondRun.Assert.ToolResult<ProbeResult>(Data.Tool.SecondCallId, Json).Scope;

        // Assert
        firstScope.Should().NotBeNull();
        secondScope.Should().NotBe(firstScope!.Value);
    }

    [Fact]
    public async Task Given_Two_Runs_Of_One_Host_Of_The_Runtime_Both_Resolve_From_Its_Scope()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Call(Data.Tool.SecondCallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        await using var probe = Runtime.Agent<ProbeAgent>();

        // Act
        var firstRun = await probe.RunAsync(Data.Tool.Question, ct: Ct);
        var secondRun = await probe.RunAsync(Data.Tool.Question, ct: Ct);
        var firstScope = firstRun.Assert.ToolResult<ProbeResult>(Data.Tool.CallId, Json).Scope;
        var secondScope = secondRun.Assert.ToolResult<ProbeResult>(Data.Tool.SecondCallId, Json).Scope;

        // Assert
        firstScope.Should().NotBeNull();
        secondScope.Should().Be(firstScope);
    }

    [Fact]
    public async Task Given_A_Host_Of_The_Runtime_Disposed_The_Scoped_Services_Of_Its_Runs_Are_Disposed()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        var probe = Runtime.Agent<ProbeAgent>();
        await probe.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await probe.DisposeAsync();

        // Assert
        _created.Should().ContainSingle();
        _created[0].Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task Given_An_Injected_Host_Disposed_The_Request_Scopes_Services_Stay_Undisposed()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        var probe = Host<ProbeAgent>();
        await probe.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await probe.DisposeAsync();

        // Assert
        _created.Should().ContainSingle();
        _created[0].Disposed.Should().BeFalse();
    }

    [Fact]
    public async Task Given_A_Runtime_From_The_Container_Disposed_The_Container_Keeps_Working()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Runtime.DisposeAsync();

        await using var probe = Runtime.Agent<ProbeAgent>();
        var response = await probe.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }
}
