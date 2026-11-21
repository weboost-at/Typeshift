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

    public ChatClientResolutionTests() => Other = CreateScriptedClient();

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
            + "no client is registered under a provider. Name a provider that has a client, register one with "
            + "services.AddKeyedChatClient(provider, client), or hand the run a provider document with "
            + "host.WithProvider(document). The default provider serves only a run that names no provider.");

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
            + "provider, and no default provider is registered: register a client for every agent with "
            + "services.AddChatClient(client) or TypeshiftRuntime.Create(client), or name a registered one with "
            + "WithDefaultProvider(key). Or name the run's provider with host.WithProvider(provider), "
            + "WithChatOptions().WithProvider(provider) or in the agent's settings.");
    }

    [Fact]
    public async Task Given_A_Default_Provider_A_Run_That_Names_None_Answers_On_Its_Client()
    {
        // Arrange
        _register = services =>
        {
            services.AddChatClient(Other);
            services.AddKeyedChatClient(Vendor.Other, ChatClient);
            services.AddTypeshift().WithDefaultProvider(Vendor.Other);
        };

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ModelOnlyAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Default_Provider_A_Run_That_Names_None_Names_It_To_Run_Middleware()
    {
        // Arrange
        var providers = SeenBy(services =>
        {
            services.AddKeyedChatClient(Vendor.Other, ChatClient);
            services.AddTypeshift().WithDefaultProvider(Vendor.Other);
        });

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ModelOnlyAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        providers.Should().Equal(Vendor.Other);
    }

    [Fact]
    public async Task Given_A_Default_Provider_Nothing_Serves_The_Client_For_Every_Agent_Does_Not_Stand_In()
    {
        // Arrange
        _register = services =>
        {
            services.AddChatClient(Other);
            services.AddTypeshift().WithDefaultProvider(Data.Agent.Provider);
        };

        // Act
        var run = () => Host<ModelOnlyAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage(
            $"*'{Data.Agent.Provider}', the default provider WithDefaultProvider names: no client is registered *");

        Other.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Default_Provider_A_Run_That_Names_Another_Runs_On_That_One()
    {
        // Arrange
        _register = services =>
        {
            services.AddKeyedChatClient(Vendor.Other, Other);
            services.AddKeyedChatClient(Vendor.Probe, ChatClient);
            services.AddTypeshift().WithDefaultProvider(Vendor.Other);
        };

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<VendorAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_No_Model_And_A_Client_Without_One_The_Run_Fails_Before_It_Stores_Anything()
    {
        // Arrange
        var bare = CreateScriptedClient(model: null);
        _register = services => services.AddChatClient(bare);
        var host = Host<SchemaAgent>().WithConversationId(Data.Agent.ConversationId);

        // Act
        var run = () => host.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage(
            $"No model is named for the agent '{SchemaAgent.AgentName}', and the client that serves it names no "
            + "model of its own in its ChatClientMetadata. Name a model with host.WithModel(model), "
            + "WithChatOptions().WithModel(model), in the agent's settings or in a provider document's Model, or "
            + "build the client with a model.");

        (await Store.LoadConversationAsync(Data.Agent.ConversationId, Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_No_Model_And_A_Client_Without_One_A_Model_At_The_Call_Site_Reaches_It()
    {
        // Arrange
        var bare = CreateScriptedClient(model: null);
        _register = services => services.AddChatClient(bare);
        bare.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<SchemaAgent>().WithModel(Data.Agent.Model).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        bare.Options[0]!.ModelId.Should().Be(Data.Agent.Model);
    }

    [Fact]
    public async Task Given_A_Document_The_Run_Answers_On_The_Client_Its_Connector_Built()
    {
        // Arrange
        _register = Connected;
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ModelOnlyAgent>().WithProvider(Document(Data.Agent.Provider)).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Connections.Single().Provider.Should().Be(Document(Data.Agent.Provider));
    }

    [Fact]
    public async Task Given_Equal_Documents_Two_Runs_Share_One_Client()
    {
        // Arrange
        _register = Connected;
        ChatClient.Reply(Text(Data.Tool.Answer)).Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ModelOnlyAgent>().WithProvider(Document(Data.Agent.Provider)).RunAsync(Data.Tool.Question, ct: Ct);
        await Host<ModelOnlyAgent>().WithProvider(Document(Data.Agent.Provider)).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Connections.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_Equal_Documents_At_The_Same_Time_The_Connector_Is_Called_Once()
    {
        // Arrange
        _register = Connected;
        Connections.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ChatClient.Reply(Text(Data.Tool.Answer)).Reply(Text(Data.Tool.Answer));

        var first = Host<ModelOnlyAgent>().WithProvider(Document(Data.Agent.Provider));
        var second = Host<ModelOnlyAgent>().WithProvider(Document(Data.Agent.Provider));
        var runs = Task.WhenAll(
            first.RunAsync(Data.Tool.Question, ct: Ct),
            second.RunAsync(Data.Tool.Question, ct: Ct));

        // Act
        Connections.Gate.SetResult();
        await runs;

        // Assert
        Connections.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Document_Whose_Key_Changed_It_Gets_A_Client_Of_Its_Own()
    {
        // Arrange
        _register = Connected;
        ChatClient.Reply(Text(Data.Tool.Answer)).Reply(Text(Data.Tool.Answer));
        await Host<ModelOnlyAgent>().WithProvider(Document(Data.Agent.Provider)).RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await Host<ModelOnlyAgent>().WithProvider(Document(Data.Agent.Model)).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Connections.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_A_Document_Of_The_Apps_Own_Type_Passed_As_That_Type_It_Is_Taken_As_A_Document()
    {
        // Arrange
        _register = Connected;
        var document = new CustomerProvider(Data.Agent.ConversationId) { Kind = "scripted", ApiKey = Data.Agent.Model };
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ModelOnlyAgent>().WithProvider(document).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Connections.Single().Provider.Should().Be(document);
    }

    [Fact]
    public async Task Given_A_Document_With_A_Model_A_Run_That_Names_None_Asks_For_It()
    {
        // Arrange
        _register = Connected;
        var document = Document(Data.Agent.Provider) with { Model = Data.Agent.Model };
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<SchemaAgent>().WithProvider(document).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ChatClient.Options[0]!.ModelId.Should().Be(Data.Agent.Model);
    }

    [Fact]
    public async Task Given_A_Document_Its_Response_Entry_Names_Its_Name_And_Not_Its_Key()
    {
        // Arrange
        _register = Connected;
        var document = Document(Data.Agent.Provider) with { Name = Data.Agent.Instructions };
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ModelOnlyAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .WithProvider(document)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        (await Store.LoadConversationAsync(Data.Agent.ConversationId, Ct))[1].Provider
            .Should()
            .Be(Data.Agent.Instructions);
    }

    [Fact]
    public async Task Given_A_Document_And_No_Connector_The_Run_Fails_Naming_The_Registration()
    {
        // Arrange
        var document = Document(Data.Agent.Provider) with { Name = Data.Agent.Instructions };

        // Act
        var run = () => Host<ModelOnlyAgent>().WithProvider(document).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage(
            $"No chat client serves the agent 'model_only_agent' on the provider '{Data.Agent.Instructions}', a "
            + "document the run was handed, since no IChatProviderConnector is registered: register the class that "
            + "turns your documents into clients with AddTypeshift().WithProviderConnector<TConnector>().");
    }

    [Fact]
    public async Task Given_A_Connector_That_Fails_The_Run_Fails_With_Its_Exception_And_Stores_Nothing()
    {
        // Arrange
        _register = services => services.AddTypeshift().WithProviderConnector<FailingConnector>();
        var host = Host<ModelOnlyAgent>().WithConversationId(Data.Agent.ConversationId);

        // Act
        var run = () => host.WithProvider(Document(Data.Agent.Provider)).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage(FailingConnector.Reason);
        (await Store.LoadConversationAsync(Data.Agent.ConversationId, Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Connector_That_Failed_The_Next_Run_Of_The_Document_Connects_Again()
    {
        // Arrange
        _register = Connected;
        Connections.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Connections.Gate.SetException(new InvalidOperationException(FailingConnector.Reason));
        await Record.ExceptionAsync(() =>
            Host<ModelOnlyAgent>().WithProvider(Document(Data.Agent.Provider)).RunAsync(Data.Tool.Question, ct: Ct));

        Connections.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Connections.Gate.SetResult();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ModelOnlyAgent>()
            .WithProvider(Document(Data.Agent.Provider))
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public void Given_A_Document_As_The_Default_Provider_The_Chain_Refuses_It()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var register = () => services.AddTypeshift().WithDefaultProvider(Document(Data.Agent.Provider));

        // Assert
        register.Should().Throw<ArgumentException>().WithParameterName("provider");
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
        };

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ModelOnlyAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ChatClient.Options[0]!.ModelId.Should().Be(ModelOnlyAgent.Model);
    }

    [Fact]
    public async Task Given_A_Model_Without_A_Provider_Settings_That_Name_A_Provider_Reach_It_With_The_Model()
    {
        // Arrange
        _register = services => services.AddTypeshift().WithScriptedProviders();
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
        _register = services => services.AddTypeshift().WithScriptedProviders();
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
    public async Task Given_Agents_Of_Two_Provider_Types_Each_Run_Takes_The_Client_Under_Its_Key()
    {
        // Arrange
        _register = services =>
        {
            services.AddTypeshift().WithScriptedProviders();
            services.AddKeyedChatClient(Vendor.Probe, new RecordingChatClient(ChatClient, Vendor.Probe, Models));
        };

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
    public async Task Given_Settings_Naming_Another_Provider_Of_An_Enum_The_Client_Under_It_Receives_The_Model()
    {
        // Arrange
        _register = services =>
            services.AddKeyedChatClient(Vendor.Other, new RecordingChatClient(ChatClient, Vendor.Other, Models));

        var settings = new VendorAgent.Settings { Provider = Vendor.Other };
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<VendorAgent>().RunAsync(Data.Tool.Question, settings, Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(Vendor.Other, VendorAgent.Model));
    }

    [Fact]
    public async Task Given_A_Configurable_Model_Without_WithProvider_Its_String_Provider_Reaches_The_Provider()
    {
        // Arrange
        _register = services => services.AddTypeshift().WithScriptedProviders();
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
    public async Task Given_Clients_Under_Keys_Of_Another_Type_The_Run_Fails_Naming_Its_Own_Key()
    {
        // Arrange
        _register = services =>
        {
            services.AddChatClient(Other);
            services.AddTypeshift().WithScriptedProviders();
        };

        // Act
        var run = () => Host<VendorAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*'{Vendor.Probe}'*");
        Other.Requests.Should().BeEmpty();
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

    private Connections Connections { get; } = [];

    private AgentHost<TAgent> Host<TAgent>() where TAgent : Agent => Scope.GetRequiredService<AgentHost<TAgent>>();

    // A document of the scripted kind, told apart from another by its key
    private static ChatProvider Document(string key) => new() { Kind = "scripted", ApiKey = key };

    // The connector that turns each document into a client of the scripted one
    private void Connected(IServiceCollection services)
    {
        services.AddTypeshift().WithProviderConnector<CountingConnector>();
        services.AddSingleton(Connections);
    }

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
