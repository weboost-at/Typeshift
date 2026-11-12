using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Testing.Fakes;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Hosting;

public class ChatClientResolutionTests : HostTestBase
{
    private Action<IServiceCollection> _register = _ => { };

    private ScriptedChatClient Other { get; } = new();

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
            services.AddScoped<IChatClientProvider, ScriptedChatClientProvider>();
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
            services.AddScoped<IChatClientProvider, ScriptedChatClientProvider>();
        };

        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(ProbeAgent.ModelProvider, ProbeAgent.Model));
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
