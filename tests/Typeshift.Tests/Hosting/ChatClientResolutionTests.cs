using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Hosting.Support;
using Typeshift.Tests.Persistence.Support;
using Typeshift.Tests.Testing.Fakes;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Hosting;

public class ChatClientResolutionTests : HostTestBase
{
    private Action<IServiceCollection> _register = _ => { };

    public ChatClientResolutionTests() => Other = new(Ct);

    private ScriptedChatClient Other { get; }

    private ProvidedModels Models { get; } = [];

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddTypeshift()
            .WithStore<InMemoryTypeshiftStore<Guid>>();

        services.AddSingleton(ChatClient);
        services.AddSingleton(Models);
        _register(services);
    }

    [Fact]
    public async Task Given_No_Model_And_A_Chat_Client_The_Run_Answers_On_The_Client()
    {
        // Arrange
        _register = services => services.AddChatClient(ChatClient);
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<SchemaAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_No_Model_And_A_Chat_Client_The_Client_Receives_No_Model()
    {
        // Arrange
        _register = services => services.AddChatClient(ChatClient);
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<SchemaAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ChatClient.Options[0]!.ModelId.Should().BeNull();
    }

    [Fact]
    public async Task Given_A_Model_And_A_Chat_Client_The_Client_Receives_The_Model()
    {
        // Arrange
        _register = services => services.AddChatClient(ChatClient);
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ChatClient.Options[0]!.ModelId.Should().Be(ProbeAgent.Model);
    }

    [Fact]
    public async Task Given_A_Chat_Client_Keyed_By_The_Agents_Provider_The_Run_Answers_On_It()
    {
        // Arrange
        _register = services =>
        {
            services.AddChatClient(Other);
            services.AddKeyedChatClient(ProbeAgent.ModelProvider, ChatClient);
        };

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Chat_Client_Provider_It_Serves_The_Agents_Model_Over_A_Chat_Client()
    {
        // Arrange
        _register = services =>
        {
            services.AddChatClient(Other);
            services.AddScoped<IChatClientProvider<string>, ScriptedChatClientProvider>();
        };

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(ProbeAgent.ModelProvider, ProbeAgent.Model));
    }

    [Fact]
    public async Task Given_A_Chat_Client_Provider_It_Serves_The_Agents_Model_Over_A_Keyed_Client()
    {
        // Arrange
        _register = services =>
        {
            services.AddKeyedChatClient(ProbeAgent.ModelProvider, Other);
            services.AddScoped<IChatClientProvider<string>, ScriptedChatClientProvider>();
        };

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(ProbeAgent.ModelProvider, ProbeAgent.Model));
    }

    [Fact]
    public async Task Given_Agents_Of_Two_Provider_Types_Each_Run_Asks_The_Chat_Client_Provider_Of_Its_Type()
    {
        // Arrange
        _register = services => services.AddTypeshift()
            .WithChatClientProvider<ScriptedChatClientProvider>()
            .WithChatClientProvider<VendorChatClientProvider>();

        ChatClient.Reply(Text(Data.Tool.Answer)).Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);
        await Host<VendorAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models.Should().Equal(
            new ProvidedModel(ProbeAgent.ModelProvider, ProbeAgent.Model),
            new ProvidedModel(Vendor.Probe, VendorAgent.Model));
    }

    [Fact]
    public async Task Given_A_Chat_Client_Provider_Of_Two_Provider_Types_It_Serves_The_Agents_Of_Both()
    {
        // Arrange
        _register = services => services.AddTypeshift().WithChatClientProvider<EveryVendorChatClientProvider>();
        ChatClient.Reply(Text(Data.Tool.Answer)).Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);
        await Host<VendorAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models.Should().Equal(
            new ProvidedModel(ProbeAgent.ModelProvider, ProbeAgent.Model),
            new ProvidedModel(Vendor.Probe, VendorAgent.Model));
    }

    [Fact]
    public async Task Given_Settings_Naming_Another_Provider_Of_An_Enum_Its_Chat_Client_Provider_Receives_It()
    {
        // Arrange
        _register = services => services.AddTypeshift().WithChatClientProvider<VendorChatClientProvider>();
        var settings = new VendorAgent.Settings { Provider = Vendor.Other };
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<VendorAgent>().RunAsync(Data.Tool.Question, settings, Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(Vendor.Other, VendorAgent.Model));
    }

    [Fact]
    public async Task Given_A_Configurable_Model_Without_WithModel_Its_String_Provider_Reaches_The_Provider()
    {
        // Arrange
        _register = services => services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();
        var settings = new EveryKindAgent.Settings { Provider = Data.Agent.Provider, Model = Data.Agent.Model };
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<EveryKindAgent>().RunAsync(Data.Tool.Question, settings, Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(Data.Agent.Provider, Data.Agent.Model));
    }

    [Fact]
    public async Task Given_A_Chat_Client_Keyed_By_An_Enum_Provider_The_Run_Answers_On_It()
    {
        // Arrange
        _register = services =>
        {
            services.AddChatClient(Other);
            services.AddKeyedChatClient(Vendor.Probe, ChatClient);
        };

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<VendorAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_Only_A_Chat_Client_Provider_Of_Another_Type_The_Run_Fails_Naming_The_Type_It_Needs()
    {
        // Arrange
        _register = services => services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();

        // Act
        var run = () => Host<VendorAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*IChatClientProvider<Vendor>*");
    }

    [Fact]
    public void Given_A_Type_That_Is_No_Chat_Client_Provider_Registering_It_Fails_Naming_TSH030()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
#pragma warning disable TSH030 // the analyzer reports it at build time; this checks the registration's own guard
        var register = () => services.AddTypeshift().WithChatClientProvider<ProvidedModels>();
#pragma warning restore TSH030

        // Assert
        register.Should().Throw<InvalidOperationException>().WithMessage("*TSH030*");
    }

    [Fact]
    public async Task Given_A_Registered_Client_With_Its_Own_Tool_Loop_The_Run_Throws_Naming_It()
    {
        // Arrange
        _register = services => services.AddChatClient(ChatClient).UseFunctionInvocation();

        // Act
        var run = () => Host<SchemaAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage(
            $"The chat client that serves the agent '{SchemaAgent.AgentName}' runs a tool loop of its own, as one "
            + "registered with UseFunctionInvocation() does. Typeshift runs the tool loop of a run itself, with "
            + "approval, tool middleware and the run's limits in it: register the client without "
            + "UseFunctionInvocation().");
    }

    [Fact]
    public async Task Given_A_Registered_Client_With_Its_Own_Tool_Loop_The_Run_Calls_No_Model()
    {
        // Arrange
        _register = services => services.AddChatClient(ChatClient).UseFunctionInvocation();

        // Act
        await Record.ExceptionAsync(() => Host<SchemaAgent>().RunAsync(Data.Tool.Question, ct: Ct));

        // Assert
        ChatClient.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_No_Model_And_No_Chat_Client_The_Run_Fails_Saying_How_To_Serve_It()
    {
        // Act
        var run = () => Host<SchemaAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*AddChatClient*");
    }

    [Fact]
    public async Task Given_A_Model_And_No_Client_For_It_The_Run_Fails_Saying_How_To_Serve_It()
    {
        // Act
        var run = () => Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*AddKeyedChatClient*");
    }

    private AgentHost<TAgent> Host<TAgent>() where TAgent : Agent => Scope.GetRequiredService<AgentHost<TAgent>>();
}
