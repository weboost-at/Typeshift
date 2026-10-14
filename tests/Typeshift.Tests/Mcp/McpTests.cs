using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Mcp.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Mcp;

public class McpTests : McpTestBase
{
    [Fact]
    public async Task Given_Mcp_Servers_Their_Tools_Are_Offered_After_The_Agents_Own()
    {
        // Arrange
        Servers["status"] = [Tool("service_status")];
        Servers["deploy"] = [Tool("deploy")];
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync();

        // Assert
        ChatClient.Options[0]!.Tools!.Select(tool => tool.Name).Should().Equal("note", "service_status", "deploy");
    }

    [Fact]
    public async Task Given_A_Server_That_Requires_Approval_A_Call_Of_Its_Tool_Asks()
    {
        // Arrange
        Servers["deploy"] = [Tool("deploy")];
        ChatClient.Reply(Call(Data.Tool.CallId, "deploy"));

        // Act
        var response = await RunAsync();

        // Assert
        response.ApprovalRequests.Should().ContainSingle();
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Read_Only_Server_Plan_Mode_Offers_Its_Tools()
    {
        // Arrange
        Servers["status"] = [Tool("service_status")];
        Servers["deploy"] = [Tool("deploy")];
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(new McpAgent.Settings { Mode = AgentMode.Plan });

        // Assert
        ChatClient.Options[0]!.Tools!.Select(tool => tool.Name).Should().Equal("service_status", "exit_plan_mode");
    }

    [Fact]
    public async Task Given_A_Server_Tool_Named_Like_The_Agents_Own_The_Run_Fails_Naming_Both()
    {
        // Arrange
        Servers["status"] = [Tool("note")];

        // Act
        var run = () => RunAsync();

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*'status'*'note'*");
    }

    [Fact]
    public async Task Given_No_Mcp_Tool_Provider_A_Run_Fails_Naming_The_Registration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        // Act
        var host = scope.ServiceProvider.GetRequiredService<AgentHost<McpAgent>>();
        var run = () => host.RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WithMcpToolProvider*");
    }

    private Task<ChatResponse> RunAsync(McpAgent.Settings? settings = null) =>
        Host<McpAgent>().RunAsync(
            Data.Tool.Question,
            conversationId: Data.Agent.ConversationId,
            settings: settings,
            ct: Ct);
}
