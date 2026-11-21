using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Compaction.Support;
using Typeshift.Tests.Hosting.Support;
using Typeshift.Tests.Testing.Fakes;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Hosting;

// D87: the provider and the model the call site hands the host, over the agent's code and its settings
public class HostSelectionTests : AgentTestBase
{
    private const string CallSiteModel = "call-site-model";

    public HostSelectionTests() => CallSite = CreateScriptedClient();

    // The client of the provider the call sites name, an enum's member no provider class serves
    private ScriptedChatClient CallSite { get; }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddKeyedChatClient(Vendor.Other, CallSite);
        services.AddTypeshift().WithProviderConnector<CountingConnector>();
        services.AddSingleton(new Connections());
    }

    [Fact]
    public async Task Given_A_Provider_At_The_Call_Site_It_Serves_The_Run_In_Place_Of_The_Agents()
    {
        // Arrange
        CallSite.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().WithProvider(Vendor.Other).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Provider_At_The_Call_Site_It_Wins_Over_The_Runs_Settings()
    {
        // Arrange
        var settings = new ProbeAgent.Settings { Provider = Data.Agent.Provider };
        CallSite.Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<ProbeAgent>().WithProvider(Vendor.Other).RunAsync(Data.Tool.Question, settings, Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Provider_At_The_Call_Site_It_Keeps_The_Model_The_Settings_Name()
    {
        // Arrange
        var settings = new ProbeAgent.Settings { Model = Data.Agent.Model };
        CallSite.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().WithProvider(Vendor.Other).RunAsync(Data.Tool.Question, settings, Ct);

        // Assert
        CallSite.Options[0]!.ModelId.Should().Be(Data.Agent.Model);
    }

    [Fact]
    public async Task Given_A_Model_At_The_Call_Site_It_Wins_Over_The_Runs_Settings()
    {
        // Arrange
        var settings = new ProbeAgent.Settings { Model = Data.Agent.Model };
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().WithModel(CallSiteModel).RunAsync(Data.Tool.Question, settings, Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(ProbeAgent.ModelProvider, CallSiteModel));
    }

    [Fact]
    public async Task Given_A_Model_At_The_Call_Site_An_Agent_Whose_Model_Is_Not_Configurable_Asks_For_It()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<CompactingAgent>().WithModel(CallSiteModel).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(CompactingAgent.ModelProvider, CallSiteModel));
    }

    [Fact]
    public async Task Given_A_Copy_With_A_Provider_The_Host_It_Came_From_Runs_On_The_Agents()
    {
        // Arrange
        var host = Host<ProbeAgent>();
        var copy = host.WithProvider(Vendor.Other);
        CallSite.Reply(Text(Data.Tool.Answer));
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await copy.RunAsync(Data.Tool.Question, ct: Ct);
        await host.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Models.Should().Equal(new ProvidedModel(ProbeAgent.ModelProvider, ProbeAgent.Model));
    }

    [Fact]
    public async Task Given_A_Copy_With_A_Provider_And_A_Model_A_Later_Copy_With_An_Id_Keeps_Both()
    {
        // Arrange
        CallSite.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>()
            .WithProvider(Vendor.Other)
            .WithModel(CallSiteModel)
            .WithSettingsId(Data.Persistence.SettingsId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        CallSite.Options[0]!.ModelId.Should().Be(CallSiteModel);
    }

    [Fact]
    public async Task Given_A_Provider_At_The_Call_Site_The_Response_Entry_Names_It()
    {
        // Arrange
        CallSite.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .WithProvider(Vendor.Other)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        (await EntriesAsync(Data.Agent.ConversationId))[1].Provider.Should().Be(nameof(Vendor.Other));
    }

    [Fact]
    public async Task Given_A_Provider_At_The_Call_Site_The_Raw_Paths_Client_Runs_On_It()
    {
        // Arrange
        CallSite.Reply(Text(Data.Tool.Answer));
        using var client = await Host<ProbeAgent>().WithProvider(Vendor.Other).CreateChatClientAsync(Ct);

        // Act
        var response = await client.GetResponseAsync(Data.Tool.Question, cancellationToken: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Host_Of_A_Kind_Its_Copy_With_A_Provider_Runs_On_It()
    {
        // Arrange
        CallSite.Reply(Text(Data.Tool.Answer));
        await using var host = Runtime.CreateHost(nameof(ProbeAgent));

        // Act
        var response = await host.WithProvider(Vendor.Other).RunAsync(Data.Tool.Question, Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Provider_At_The_Call_Site_Nothing_Serves_The_Run_Fails_Naming_It()
    {
        // Act
        var run = () => Host<ProbeAgent>().WithProvider(Vendor.Probe).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"*'{ProbeAgent.Model}' of '{Vendor.Probe}': the registered clients are under 'Other'*");
    }

    [Fact]
    public async Task Given_A_Document_At_The_Call_Site_It_Wins_Over_The_Runs_Settings()
    {
        // Arrange
        var settings = new ProbeAgent.Settings { Provider = Data.Agent.Provider };
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>()
            .WithProvider(new ChatProvider { Kind = "scripted", ApiKey = Data.Agent.Model })
            .RunAsync(Data.Tool.Question, settings, Ct);

        // Assert
        Models.Should().BeEmpty();
    }

    [Fact]
    public void Given_A_Document_Without_A_Kind_At_The_Call_Site_The_Copy_Is_Refused()
    {
        // Act
        var copy = () => Host<ProbeAgent>().WithProvider(new ChatProvider { Kind = " " });

        // Assert
        copy.Should().Throw<ArgumentException>().WithParameterName("provider");
    }

    [Fact]
    public void Given_A_Blank_Model_At_The_Call_Site_The_Copy_Is_Refused()
    {
        // Act
        var copy = () => Host<ProbeAgent>().WithModel(" ");

        // Assert
        copy.Should().Throw<ArgumentException>().WithParameterName("model");
    }
}
