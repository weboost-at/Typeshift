using Typeshift.Agents;
using Typeshift.Conversations;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Toolsets.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Agents;

public class AgentRuntimeTests : AgentTestBase
{
    [Fact]
    public void Given_Run_Settings_Their_Chat_Options_Apply_Over_The_Code()
    {
        // Arrange
        var run = new ProbeAgent.Settings { Temperature = Agent.Temperature };

        // Act
        var options = Runtime.ResolveChatOptions(run);

        // Assert
        options.Should().BeEquivalentTo(new { Temperature = Agent.Temperature.Default, AllowMultipleToolCalls = true });
    }

    [Fact]
    public void Given_An_Unset_Run_Option_The_Agents_Default_Stays()
    {
        // Act
        var options = Runtime.ResolveChatOptions(new ProbeAgent.Settings());

        // Assert
        options.Temperature.Should().Be(0.2f);
    }

    [Fact]
    public void Given_Instructions_In_Code_The_Options_Carry_Them()
    {
        // Act
        var options = Runtime.ResolveChatOptions();

        // Assert
        options.Instructions.Should().Be(ProbeAgent.CodeInstructions);
    }

    [Fact]
    public void Given_Run_Instructions_They_Apply_Over_The_Code()
    {
        // Arrange
        var run = new ProbeAgent.Settings { Instructions = Agent.Instructions };

        // Act
        var options = Runtime.ResolveChatOptions(run);

        // Assert
        options.Instructions.Should().Be(Agent.Instructions);
    }

    [Fact]
    public void Given_A_Conversation_Id_The_Options_Carry_It()
    {
        // Act
        var options = Runtime.ResolveChatOptions(conversationId: Agent.ConversationId.ToString());

        // Assert
        options.ConversationId.Should().Be(Agent.ConversationId.ToString());
    }

    [Fact]
    public void Given_A_Run_The_Agent_Runs_In_Its_Own_Execution_Context()
    {
        // Act
        var options = Runtime.ResolveChatOptions();

        // Assert
        options.AdditionalProperties!.GetExecutionContext().Should().Be(new ChatExecutionContext("probe_agent"));
    }

    [Fact]
    public async Task Given_The_Agents_Tool_Settings_Its_Tools_Run_With_All_Of_Them()
    {
        // Act
        var settings = await ProbeAsync(Runtime.ResolveChatOptions());

        // Assert
        settings.Should().Be(new ProbeSettings { Limit = 5, Mode = "thorough" });
    }

    [Fact]
    public async Task Given_Run_Tool_Settings_Their_Values_Apply_Over_The_Agents_Own()
    {
        // Arrange
        var run = new ProbeAgent.Settings
        {
            Probe = new ProbeToolset.ProbeOverrides { Limit = Data.Tool.Limit }
        };

        // Act
        var settings = await ProbeAsync(Runtime.ResolveChatOptions(run));

        // Assert
        settings.Should().Be(new ProbeSettings { Limit = Data.Tool.Limit.Default, Mode = "thorough" });
    }

    [Fact]
    public async Task Given_Run_Tool_Settings_The_Next_Run_Uses_The_Agents_Own()
    {
        // Arrange
        var run = new ProbeAgent.Settings
        {
            Probe = new ProbeToolset.ProbeOverrides { Limit = Data.Tool.Limit }
        };

        Runtime.ResolveChatOptions(run);

        // Act
        var settings = await ProbeAsync(Runtime.ResolveChatOptions());

        // Assert
        settings.Should().Be(new ProbeSettings { Limit = 5, Mode = "thorough" });
    }

    [Fact]
    public async Task Given_Tools_In_The_Agents_Chat_Options_The_Run_Offers_Them_Before_Its_Toolsets()
    {
        // Arrange
        var runtime = Services.GetRequiredService<AgentRuntime<HostedToolAgent>>();

        // Act
        var options = runtime.ResolveChatOptions();

        // Assert
        options.Tools!.Select(tool => tool.Name).Should().Equal("web_search", "probe");
    }

    [Fact]
    public async Task Given_No_Model_Creating_A_Chat_Client_Throws_A_ConfigurationRequiredException()
    {
        // Act
        var create = () => Runtime.CreateChatClientAsync(Runtime.ResolveChatOptions(), Ct);

        // Assert
        await create.Should().ThrowAsync<ConfigurationRequiredException>();
    }

    [Fact]
    public async Task Given_A_Model_Its_Chat_Client_Stores_The_Conversation_Under_The_Agent()
    {
        // Arrange
        ChatClient.Reply(Text(Agent.Instructions));
        await Runtime.ConfigureAsync(Agent.Id.Default, settings => settings with { Model = Agent.Model }, Ct);

        var conversationId = Agent.ConversationId.ToString();
        var options = await Runtime.ResolveChatOptionsAsync(Agent.Id.Default, conversationId: conversationId, ct: Ct);
        var client = await Runtime.CreateChatClientAsync(options, Ct);

        // Act
        await client.GetResponseAsync(Data.Tool.Query, options, Ct);
        var stored = await Store.LoadAsync<StoredConversation>(Agent.ConversationId, Ct);

        // Assert
        stored!.ExecutionContext.Should().Be(new ChatExecutionContext("probe_agent"));
    }
}
