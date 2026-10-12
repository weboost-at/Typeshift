using Agentic.Agents;
using Agentic.Tests.Agents.Support;
using Agentic.Tests.Toolsets.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Tests.Agents;

public class AgentRuntimeTests : AgentTestBase
{
    [Fact]
    public async Task Given_Run_Chat_Options_Only_The_Allowlisted_Ones_Change()
    {
        // Arrange
        var run = new RunOverrides
        {
            ChatOptions = new ChatOptions
            {
                Temperature = Agent.Temperature,
                AllowMultipleToolCalls = false
            }
        };

        // Act
        var options = await Runtime.ResolveChatOptionsAsync(run, Ct);

        // Assert
        options.Should().BeEquivalentTo(new { Temperature = Agent.Temperature.Default, AllowMultipleToolCalls = true });
    }

    [Fact]
    public async Task Given_An_Unset_Allowlisted_Run_Option_The_Agents_Default_Stays()
    {
        // Arrange
        var run = new RunOverrides
        {
            ChatOptions = new ChatOptions()
        };

        // Act
        var options = await Runtime.ResolveChatOptionsAsync(run, Ct);

        // Assert
        options.Temperature.Should().Be(0.2f);
    }

    [Fact]
    public async Task Given_Run_Chat_Options_With_A_Conversation_The_Conversation_Id_Is_Kept()
    {
        // Arrange
        var run = new RunOverrides
        {
            ChatOptions = new ChatOptions { ConversationId = Agent.ConversationId }
        };

        // Act
        var options = await Runtime.ResolveChatOptionsAsync(run, Ct);

        // Assert
        options.ConversationId.Should().Be(Agent.ConversationId);
    }

    [Fact]
    public async Task Given_Run_Chat_Options_Of_Another_Execution_Context_The_Agent_Runs_In_Its_Own()
    {
        // Arrange
        var properties = new AdditionalPropertiesDictionary().WithExecutionContext(new(Agent.OtherContext));

        var run = new RunOverrides
        {
            ChatOptions = new ChatOptions { AdditionalProperties = properties }
        };

        // Act
        var options = await Runtime.ResolveChatOptionsAsync(run, Ct);

        // Assert
        options.AdditionalProperties!.GetExecutionContext().Should().Be(new ChatExecutionContext("probe_agent"));
    }

    [Fact]
    public async Task Given_The_Agents_Tool_Settings_Its_Tools_Run_With_All_Of_Them()
    {
        // Arrange
        var options = await Runtime.ResolveChatOptionsAsync(ct: Ct);

        // Act
        var settings = await ProbeAsync(options);

        // Assert
        settings.Should().Be(new ProbeSettings { Limit = 5, Mode = "thorough" });
    }

    [Fact]
    public async Task Given_Run_Tool_Settings_Only_Their_Allowlisted_Members_Change()
    {
        // Arrange
        var settings = new ProbeSettings
        {
            Limit = Data.Tool.Limit,
            Mode = Data.Tool.Mode
        };

        var run = new RunOverrides
        {
            ToolSettings = [settings]
        };

        var options = await Runtime.ResolveChatOptionsAsync(run, Ct);

        // Act
        var resolved = await ProbeAsync(options);

        // Assert
        resolved.Should().Be(new ProbeSettings { Limit = Data.Tool.Limit.Default, Mode = "thorough" });
    }

    [Fact]
    public async Task Given_Run_Tool_Settings_The_Next_Run_Uses_The_Agents_Own()
    {
        // Arrange
        var run = new RunOverrides
        {
            ToolSettings = [new ProbeSettings { Limit = Data.Tool.Limit }]
        };

        await Runtime.ResolveChatOptionsAsync(run, Ct);
        var next = await Runtime.ResolveChatOptionsAsync(ct: Ct);

        // Act
        var settings = await ProbeAsync(next);

        // Assert
        settings.Should().Be(new ProbeSettings { Limit = 5, Mode = "thorough" });
    }

    [Fact]
    public async Task Given_No_Model_Creating_A_Chat_Client_Throws_A_ConfigurationRequiredException()
    {
        // Act
        var create = () => Runtime.CreateChatClientAsync(Ct);

        // Assert
        await create.Should().ThrowAsync<ConfigurationRequiredException>();
    }

    [Fact]
    public async Task Given_A_Model_Its_Chat_Client_Stores_The_Conversation_Under_The_Agent()
    {
        // Arrange
        ChatClient.Reply(Text(Agent.Instructions));
        await Runtime.UpdateConfigurationAsync(configuration => configuration with { Model = Agent.Model }, Ct);

        var run = new RunOverrides
        {
            ChatOptions = new ChatOptions { ConversationId = Agent.ConversationId }
        };

        var options = await Runtime.ResolveChatOptionsAsync(run, Ct);
        var client = await Runtime.CreateChatClientAsync(Ct);

        // Act
        await client.GetResponseAsync(Data.Tool.Query, options, Ct);
        var stored = await Conversations.LoadAsync(Guid.Parse(Agent.ConversationId), Ct);

        // Assert
        stored!.ExecutionContext.Should().Be(new ChatExecutionContext(ProbeAgent.Name));
    }

    [Fact]
    public async Task Given_Tools_In_The_Agents_Chat_Options_The_Run_Offers_Them_Before_Its_Toolsets()
    {
        // Arrange
        var runtime = Services.GetRequiredService<AgentRuntime<HostedToolAgent>>();

        // Act
        var options = await runtime.ResolveChatOptionsAsync(ct: Ct);

        // Assert
        options.Tools!.Select(tool => tool.Name).Should().Equal("web_search", "probe");
    }
}
