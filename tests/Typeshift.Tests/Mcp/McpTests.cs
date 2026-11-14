using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Approval.Support;
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
        response.Decisions.OfType<ApprovalDecision>().Select(decision => decision.Server).Should().Equal("deploy");
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Servers_Tool_Approved_For_The_Conversation_Its_Standing_Approval_Names_The_Server()
    {
        // Arrange
        Servers["deploy"] = [Tool("deploy")];

        ChatClient
            .Reply(Call(Data.Tool.CallId, "deploy"))
            .Reply(Call(Data.Tool.SecondCallId, "deploy"))
            .Reply(Text(Data.Tool.Answer));

        var chat = Host<McpAgent>().WithConversationId(Data.Agent.ConversationId);
        var asked = await chat.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await chat.AnswerAsync([DecisionAnswer.ApproveForConversation(asked.Decisions[0].Id)], ct: Ct);
        var decisions = await chat.GetDecisionsAsync(Ct);

        // Assert
        decisions.Standing.Select(standing => (standing.Tool, standing.Server)).Should().Equal(("deploy", "deploy"));
        ToolCalls.Should().Equal("deploy", "deploy");
    }

    [Fact]
    public async Task Given_A_Servers_Tool_Approved_For_The_Conversation_A_Toolset_Tool_Of_Its_Name_Still_Asks()
    {
        // Arrange
        Servers["deploy"] = [Tool("close_ticket")];

        ChatClient
            .Reply(Call(Data.Tool.CallId, "close_ticket"))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Call(Data.Tool.SecondCallId, "close_ticket", ("number", Data.Agent.Ticket.Default)));

        var chat = Host<McpAgent>().WithConversationId(Data.Agent.ConversationId);
        var asked = await chat.RunAsync(Data.Tool.Question, ct: Ct);
        await chat.AnswerAsync([DecisionAnswer.ApproveForConversation(asked.Decisions[0].Id)], ct: Ct);

        // Act
        var later = await Host<GuardedAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        later.Decisions.Should().ContainSingle();
        ToolCalls.Should().Equal("close_ticket");
    }

    [Fact]
    public async Task Given_A_Tool_Approved_For_The_Conversation_A_Sub_Agents_Server_Tool_Of_That_Name_Does_Not_Run()
    {
        // Arrange
        Servers["deploy"] = [Tool("close_ticket")];

        ChatClient
            .Reply(Call(Data.Tool.CallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Call(Data.Tool.SecondCallId, "mcp_helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Call($"{Data.Tool.CallId}_helper", "close_ticket"))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        var lead = Host<McpLeadAgent>().WithConversationId(Data.Agent.ConversationId);
        var asked = await lead.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        await lead.AnswerAsync([DecisionAnswer.ApproveForConversation(asked.Decisions[0].Id)], ct: Ct);

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_A_Tool_Approved_For_The_Conversation_A_Server_Tool_Of_That_Name_After_A_Handoff_Asks()
    {
        // Arrange
        Servers["deploy"] = [Tool("close_ticket")];

        ChatClient
            .Reply(Call(Data.Tool.CallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Call(Data.Tool.SecondCallId, "handoff_to_mcp_back_agent"))
            .Reply(Call($"{Data.Tool.CallId}_back", "close_ticket"));

        var front = Host<McpFrontAgent>().WithConversationId(Data.Agent.ConversationId);
        var asked = await front.RunAsync(Data.Tool.Question, ct: Ct);

        // Act
        var handed = await front.AnswerAsync([DecisionAnswer.ApproveForConversation(asked.Decisions[0].Id)], ct: Ct);

        // Assert
        handed.Decisions.OfType<ApprovalDecision>().Select(decision => decision.Server).Should().Equal("deploy");
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_A_Servers_Tool_Approved_For_The_Conversation_Withdrawing_It_Without_The_Server_Keeps_It()
    {
        // Arrange
        var chat = await ApprovedForTheConversationAsync();

        // Act
        await chat.RevokeStandingApprovalAsync("deploy", ct: Ct);
        var decisions = await chat.GetDecisionsAsync(Ct);

        // Assert
        decisions.Standing.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Servers_Tool_Approved_For_The_Conversation_Withdrawing_It_With_The_Server_Makes_It_Ask()
    {
        // Arrange
        var chat = await ApprovedForTheConversationAsync();
        ChatClient.Reply(Call(Data.Tool.SecondCallId, "deploy"));

        // Act
        await chat.RevokeStandingApprovalAsync("deploy", "deploy", Ct);
        var later = await chat.RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        later.Decisions.Should().ContainSingle();
        ToolCalls.Should().Equal("deploy");
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
    public async Task Given_A_Server_Tool_Named_Exit_Plan_Mode_The_Run_Fails_Naming_The_Server_And_Calls_No_Model()
    {
        // Arrange
        Servers["status"] = [Tool("exit_plan_mode")];

        // Act
        var run = () => RunAsync(new McpAgent.Settings { Mode = AgentMode.Plan });

        // Assert
        await run.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*'exit_plan_mode' through the MCP server 'status'*reserved for the tool that presents*");
        ChatClient.Requests.Should().BeEmpty();
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

    // A conversation whose user approved the deploy server's tool for all of it
    private async Task<AgentHost<McpAgent>> ApprovedForTheConversationAsync()
    {
        Servers["deploy"] = [Tool("deploy")];

        ChatClient
            .Reply(Call(Data.Tool.CallId, "deploy"))
            .Reply(Text(Data.Tool.Answer));

        var chat = Host<McpAgent>().WithConversationId(Data.Agent.ConversationId);
        var asked = await chat.RunAsync(Data.Tool.Question, ct: Ct);
        await chat.AnswerAsync([DecisionAnswer.ApproveForConversation(asked.Decisions[0].Id)], ct: Ct);

        return chat;
    }

    private Task<ChatResponse> RunAsync(McpAgent.Settings? settings = null) =>
        Host<McpAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(
            Data.Tool.Question,
            settings: settings,
            ct: Ct);
}
