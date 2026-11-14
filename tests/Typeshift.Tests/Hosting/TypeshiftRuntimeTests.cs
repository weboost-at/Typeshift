using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Conversations.Support;
using Typeshift.Tests.Hosting.Support;
using Typeshift.Tests.MultiAgent.Support;
using Typeshift.Tests.Testing.Fakes;
using Typeshift.Tests.Tools.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Typeshift.Tests.Hosting;

// D75, D76: the runtime TypeshiftRuntime.Create builds, and the one AddTypeshift() registers
public class TypeshiftRuntimeTests : AgentTestBase
{
    private readonly CreatedMiddleware _created = [];

    private readonly Builds _hostBuilds = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddTypeshift()
            .WithRunMiddleware<DisposableMiddleware>();

        services.AddSingleton(_created);
        services.CountBuilds<AgentHost<HelperAgent>>(_hostBuilds);
        services.CountBuilds<AgentHost<FrontAgent>>(_hostBuilds);
        services.CountBuilds<AgentHost<BackAgent>>(_hostBuilds);
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
            typeshift => typeshift.WithStore<InMemoryTypeshiftStore<Guid>>());

        await using var probe = runtime.Agent<ProbeAgent>();
        await using var configuring = runtime.Agent<ProbeAgent>();

        await configuring.WithSettingsId(Data.Persistence.SettingsId).ConfigureAsync(
            settings => settings with { Temperature = Data.Agent.Temperature },
            Ct);

        // Act
        var options = await probe.WithSettingsId(Data.Persistence.SettingsId).ResolveOptionsAsync(ct: Ct);

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
        await runtime.DisposeAsync();

        // Assert
        store.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task Given_A_Runtime_Disposed_Synchronously_After_A_Run_Its_Containers_Singletons_Are_Disposed()
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
    public async Task Given_A_Disposed_Runtime_Creating_A_Host_Throws()
    {
        // Arrange
        var runtime = TypeshiftRuntime.Create(ChatClient);
        await runtime.DisposeAsync();

        // Act
        var host = () => runtime.Agent<ProbeAgent>();

        // Assert
        host.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Given_A_Disposed_Runtime_Disposing_It_Again_Throws_Nothing()
    {
        // Arrange
        var runtime = TypeshiftRuntime.Create(ChatClient);
        await runtime.DisposeAsync();

        // Act
        var dispose = async () => await runtime.DisposeAsync();

        // Assert
        await dispose.Should().NotThrowAsync();
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
    public async Task Given_A_Service_Added_Through_The_Chain_A_Tool_Resolves_It()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        await using var runtime = TypeshiftRuntime.Create(
            ChatClient,
            typeshift => typeshift.Services.AddScoped<CallScope>());

        await using var probe = runtime.Agent<ProbeAgent>();

        // Act
        var response = await probe.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Assert.ToolResult<ProbeResult>(Data.Tool.CallId, Json).Scope.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_A_Logger_Added_Through_The_Chain_A_Failed_Append_Is_Logged()
    {
        // Arrange
        var log = new ErrorLog();
        ChatClient.Reply(Text(Data.Tool.Answer));

        await using var runtime = TypeshiftRuntime.Create(
            ChatClient,
            typeshift =>
            {
#pragma warning disable TSH036 // the store fails every append, and the run never asks
                typeshift.WithStore<FailingAppendStore>();
#pragma warning restore TSH036
                typeshift.Services.AddSingleton<ILoggerFactory>(log);
            });

        await using var probe = runtime.Agent<ProbeAgent>();

        // Act
        await probe.WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        log.Errors.Should().Contain(error => error.Exception is IOException);
    }

    [Fact]
    public async Task Given_A_Logger_Added_Through_The_Chain_A_Tools_Exception_Is_Logged()
    {
        // Arrange
        var log = new ErrorLog();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "crash", ("message", Data.Agent.Reason)))
            .Reply(Text(Data.Tool.Answer));

        await using var runtime = TypeshiftRuntime.Create(
            ChatClient,
            typeshift => typeshift.Services.AddSingleton<ILoggerFactory>(log));

        await using var failing = runtime.Agent<FailingAgent>();

        // Act
        await failing.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        log.Errors.Should().ContainSingle(error => error.Exception.Message == Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Logger_Added_Through_The_Chain_The_First_Save_Without_A_Store_Warns()
    {
        // Arrange
        var log = new WarningLog();
        ChatClient.Reply(Text(Data.Tool.Answer));

        await using var runtime = TypeshiftRuntime.Create(
            ChatClient,
            typeshift => typeshift.Services.AddSingleton<ILoggerFactory>(log));

        await using var probe = runtime.Agent<ProbeAgent>();

        // Act
        await probe.WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        log.Warnings.Should().ContainSingle(warning => warning.Contains("WithStore"));
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_A_Host_Resolved_From_The_Root_Provider_It_Fails_Naming_The_Runtime(bool validateScopes)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);
        await using var provider = services.BuildServiceProvider(validateScopes);

        // Act
        var resolve = () => provider.GetRequiredService<AgentHost<ProbeAgent>>();

        // Assert
        resolve.Should().Throw<InvalidOperationException>().WithMessage(
            "AgentHost<ProbeAgent> was resolved from the root provider, as a host injected into a singleton is. Its "
            + "runs would share one instance of each scoped service, such as the chat client provider of one "
            + "customer, and the root would keep every host until the app stops: inject TypeshiftRuntime and call "
            + "Agent<ProbeAgent>() for each unit of work, or resolve the host from a scope.");
    }

    [Fact]
    public async Task Given_A_Runtime_Of_Create_A_Host_Resolved_From_Its_Services_Fails_Naming_The_Runtime()
    {
        // Arrange
        await using var runtime = TypeshiftRuntime.Create(ChatClient);

        // Act
        var resolve = () => runtime.Services.GetRequiredService<AgentHost<ProbeAgent>>();

        // Assert
        resolve.Should().Throw<InvalidOperationException>().WithMessage("*Agent<ProbeAgent>()*");
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
    public async Task Given_A_Host_Of_The_Runtime_Disposed_Synchronously_The_Scoped_Services_Of_Its_Runs_Are_Disposed()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        var probe = Runtime.Agent<ProbeAgent>();
        await probe.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        probe.Dispose();

        // Assert
        _created.Should().ContainSingle();
        _created[0].Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task Given_A_Disposed_Host_Of_The_Runtime_Disposing_It_Again_Throws_Nothing()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        var probe = Runtime.Agent<ProbeAgent>();
        await probe.RunAsync(Data.Tool.Question, ct: Ct);
        await probe.DisposeAsync();

        // Act
        var dispose = () => probe.Dispose();

        // Assert
        dispose.Should().NotThrow();
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
    public async Task Given_A_Copy_Of_A_Host_Of_The_Runtime_Disposed_The_Host_Still_Runs_In_Its_Scope()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Call(Data.Tool.SecondCallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        await using var probe = Runtime.Agent<ProbeAgent>();
        var copy = probe.WithConversationId(Data.Agent.ConversationId);
        var copyRun = await copy.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await copy.DisposeAsync();
        var hostRun = await probe.RunAsync(Data.Tool.Question, ct: Ct);
        var copyScope = copyRun.Assert.ToolResult<ProbeResult>(Data.Tool.CallId, Json).Scope;
        var hostScope = hostRun.Assert.ToolResult<ProbeResult>(Data.Tool.SecondCallId, Json).Scope;

        // Assert
        copyScope.Should().NotBeNull();
        hostScope.Should().Be(copyScope);
    }

    [Fact]
    public async Task Given_A_Copy_Of_A_Host_Of_The_Runtime_Disposed_The_Scoped_Services_Of_Its_Runs_Stay_Undisposed()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        await using var probe = Runtime.Agent<ProbeAgent>();
        var copy = probe.WithConversationId(Data.Agent.ConversationId);
        await copy.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await copy.DisposeAsync();

        // Assert
        _created.Should().ContainSingle();
        _created[0].Disposed.Should().BeFalse();
    }

    [Fact]
    public async Task Given_A_Host_Of_The_Runtime_Disposed_After_Its_Copy_The_Scoped_Services_Are_Disposed_Once()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        var probe = Runtime.Agent<ProbeAgent>();
        var copy = probe.WithConversationId(Data.Agent.ConversationId);
        await copy.RunAsync(Data.Tool.Question, ct: Ct);
        await copy.DisposeAsync();

        // Act
        await probe.DisposeAsync();

        // Assert
        _created.Should().ContainSingle();
        _created[0].Disposals.Should().Be(1);
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

    [Fact]
    public async Task Given_Runs_Of_One_Host_That_Call_A_Sub_Agent_The_Container_Builds_No_Host_Its_Scope_Keeps()
    {
        // Arrange
        for (var run = 0; run < 3; run++)
        {
            ChatClient
                .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
                .Reply(Text(Data.Agent.Reason))
                .Reply(Text(Data.Tool.Answer));
        }

        await using var lead = Runtime.Agent<LeadAgent>();

        // Act
        for (var run = 0; run < 3; run++)
        {
            await lead.RunAsync(Data.Tool.Question, ct: Ct);
        }

        // Assert
        _hostBuilds.Count.Should().Be(0);
    }

    [Fact]
    public async Task Given_Runs_Of_One_Host_That_Offer_A_Handoff_The_Container_Builds_No_Host_Its_Scope_Keeps()
    {
        // Arrange
        for (var run = 0; run < 3; run++)
        {
            ChatClient.Reply(Text(Data.Tool.Answer));
        }

        await using var front = Runtime.Agent<FrontAgent>();

        // Act
        for (var run = 0; run < 3; run++)
        {
            await front.WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);
        }

        // Assert
        _hostBuilds.Count.Should().Be(0);
    }
}
