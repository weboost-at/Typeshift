using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Compaction.Support;
using Typeshift.Tests.Hosting.Support;
using Typeshift.Tests.Middleware.Support;
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

    private ITypeshiftStore<Guid> Store => Services.GetRequiredService<ITypeshiftStore<Guid>>();

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
    public async Task Given_A_Provider_Nothing_Serves_The_Default_Client_Does_Not_Serve_It()
    {
        // Arrange
        _register = services => services.AddChatClient(ChatClient);

        // Act
        var run = () => Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage(
            $"No chat client serves the agent 'probe_agent' on '{ProbeAgent.Model}' of '{ProbeAgent.ModelProvider}': "
            + "no client is registered under a provider, and no IChatClientProvider<string> is registered. Name a "
            + "provider that has a client, register one with services.AddKeyedChatClient(provider, client), or "
            + "register a provider class with WithChatClientProvider<T>(). The default client serves only a run "
            + "that names no provider, as an agent with WithChatOptions().WithModel(model) does.");

        ChatClient.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Provider_Nothing_Serves_Beside_Keyed_Clients_The_Run_Fails_Naming_The_Registered_Ones()
    {
        // Arrange
        _register = services =>
        {
            services.AddChatClient(Other);
            services.AddKeyedChatClient(Data.Agent.Provider, Other);
            services.AddKeyedChatClient(Vendor.Other, Other);
        };

        // Act
        var run = () => Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"*'{ProbeAgent.ModelProvider}'*under '{Data.Agent.Provider}', 'Other'*");
    }

    [Fact]
    public async Task Given_A_Provider_Nothing_Serves_The_Run_Stores_No_Entry_And_Frees_Its_Conversation()
    {
        // Arrange
        _register = services => services.AddChatClient(Other);
        var host = Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId);
        await Record.ExceptionAsync(() => host.RunAsync(Data.Tool.Question, ct: Ct));

        // Act
        var again = () => host.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await again.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*'{ProbeAgent.ModelProvider}'*");
        (await Store.LoadConversationAsync(Data.Agent.ConversationId, Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Settings_Naming_A_Provider_In_Another_Case_The_Run_Fails_Naming_The_Registered_One()
    {
        // Arrange
        _register = services =>
        {
            services.AddChatClient(Other);
            services.AddKeyedChatClient(ProbeAgent.ModelProvider, ChatClient);
        };

        var settings = new ProbeAgent.Settings { Provider = ProbeAgent.ModelProvider.ToUpperInvariant() };

        // Act
        var run = () => Host<ProbeAgent>().RunAsync(Data.Tool.Question, settings, Ct);

        // Assert
        await run.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"*under '{ProbeAgent.ModelProvider}'*");
    }

    [Fact]
    public async Task Given_No_Provider_And_Only_Keyed_Clients_The_Run_Fails_Naming_The_Default_Client()
    {
        // Arrange
        _register = services => services.AddKeyedChatClient(ProbeAgent.ModelProvider, Other);

        // Act
        var run = () => Host<ModelOnlyAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage(
            $"No chat client serves the agent 'model_only_agent' on '{ModelOnlyAgent.Model}', which names no "
            + "provider, and no default client is registered: register one for every agent with "
            + "services.AddChatClient(client) or TypeshiftRuntime.Create(client), or name a provider with "
            + "WithChatOptions().WithModel(provider, model) or in the agent's settings.");
    }

    [Fact]
    public async Task Given_A_Keyed_Client_And_A_Provider_Without_A_Model_The_Client_Answers_On_Its_Own_Model()
    {
        // Arrange
        _register = services => services.AddKeyedChatClient(Data.Agent.Provider, ChatClient);
        var settings = new EveryKindAgent.Settings { Provider = Data.Agent.Provider };
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<EveryKindAgent>().RunAsync(Data.Tool.Question, settings, Ct);

        // Assert
        ChatClient.Options[0]!.ModelId.Should().BeNull();
    }

    [Fact]
    public async Task Given_A_Chat_Client_Provider_And_A_Provider_Without_A_Model_The_Run_Fails_Naming_The_Model()
    {
        // Arrange
        _register = services =>
        {
            services.AddChatClient(Other);
            services.AddKeyedChatClient(Data.Agent.Provider, Other);
            services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();
        };

        var settings = new EveryKindAgent.Settings { Provider = Data.Agent.Provider };

        // Act
        var run = () => Host<EveryKindAgent>().RunAsync(Data.Tool.Question, settings, Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage(
            $"No chat client serves the agent 'every_kind_agent' on '{Data.Agent.Provider}', which names no model: "
            + "the IChatClientProvider<string> that is registered serves a provider and a model. Name the model "
            + "too, in the agent's code or its settings.");

        Other.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Summary_Provider_Nothing_Serves_A_Run_That_Compacts_Fails_Before_Its_First_Call()
    {
        // Arrange
        _register = services => services.AddKeyedChatClient(CompactingAgent.ModelProvider, ChatClient);

        var settings = new CompactingAgent.Settings
        {
            MaxInputTokens = 1_000,
            SummaryProvider = Data.Agent.Provider,
            SummaryModel = Data.Agent.Model
        };

        // Act
        var run = () => Host<CompactingAgent>().RunAsync(Data.Tool.Question, settings, Ct);

        // Assert
        await run.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"*the summary of the agent*'{Data.Agent.Model}' of '{Data.Agent.Provider}'*");

        ChatClient.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Run_On_The_Default_Client_It_Names_No_Provider_To_Run_Middleware()
    {
        // Arrange
        var providers = SeenBy(services => services.AddChatClient(ChatClient));
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ModelOnlyAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        providers.Should().Equal([null]);
    }

    [Fact]
    public async Task Given_A_Run_On_A_Provider_Of_An_Enum_It_Names_The_Enums_Value_To_Run_Middleware()
    {
        // Arrange
        var providers = SeenBy(services => services.AddKeyedChatClient(Vendor.Probe, ChatClient));
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<VendorAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        providers.Should().Equal(Vendor.Probe);
    }

    [Fact]
    public async Task Given_A_Run_On_The_Default_Client_Its_Response_Entry_Carries_The_Model_And_No_Provider()
    {
        // Arrange
        _register = services => services.AddChatClient(ChatClient);
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ModelOnlyAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        var response = (await Store.LoadConversationAsync(Data.Agent.ConversationId, Ct))[1];

        // Assert
        (response.Model, response.Provider).Should().Be((ModelOnlyAgent.Model, null));
    }

    [Fact]
    public async Task Given_A_Summary_Provider_Nothing_Serves_A_Run_That_Never_Compacts_Answers()
    {
        // Arrange
        _register = services => services.AddKeyedChatClient(CompactingAgent.ModelProvider, ChatClient);
        var settings = new CompactingAgent.Settings { SummaryProvider = Data.Agent.Provider };
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<CompactingAgent>().RunAsync(Data.Tool.Question, settings, Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Model_Without_A_Provider_The_Client_For_Every_Agent_Receives_The_Model()
    {
        // Arrange
        _register = services =>
        {
            services.AddChatClient(ChatClient);
            services.AddKeyedChatClient(ProbeAgent.ModelProvider, Other);
            services.AddScoped<IChatClientProvider<string>, ScriptedChatClientProvider>();
        };

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ModelOnlyAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ChatClient.Options[0]!.ModelId.Should().Be(ModelOnlyAgent.Model);
        Models.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Model_Without_A_Provider_Settings_That_Name_A_Provider_Reach_It_With_The_Model()
    {
        // Arrange
        _register = services => services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();
        var settings = new ModelOnlyAgent.Settings { Provider = Data.Agent.Provider };
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ModelOnlyAgent>().RunAsync(Data.Tool.Question, settings, Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(Data.Agent.Provider, ModelOnlyAgent.Model));
    }

    [Fact]
    public async Task Given_A_Model_Named_After_A_Provider_And_Its_Model_The_Provider_Serves_The_Later_Model()
    {
        // Arrange
        _register = services => services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<SwitchedModelAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(SwitchedModelAgent.ModelProvider, SwitchedModelAgent.Model));
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
    public async Task Given_A_Chat_Client_Provider_Of_Another_Type_The_Run_Fails_Naming_The_Type_It_Needs()
    {
        // Arrange
        _register = services =>
        {
            services.AddChatClient(Other);
            services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();
        };

        // Act
        var run = () => Host<VendorAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*IChatClientProvider<Vendor>*");
        Other.Requests.Should().BeEmpty();
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

    // The provider each run names to run middleware, over the clients a test registers
    private List<object?> SeenBy(Action<IServiceCollection> register)
    {
        var script = new MiddlewareScript();
        var providers = new List<object?>();

        script.Runs[0] = (run, next) =>
        {
            providers.Add(run.Provider);
            return next();
        };

        _register = services =>
        {
            register(services);
            services.AddSingleton(script);
            services.AddTypeshift().WithRunMiddleware<FirstRunMiddleware>();
        };

        return providers;
    }
}
