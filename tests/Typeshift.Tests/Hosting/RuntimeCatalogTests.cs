using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Approval.Support;
using Typeshift.Tests.Hosting.Support;
using Typeshift.Tests.Persistence.Support;
using Typeshift.Tests.Tools.Support;
using Typeshift.Tests.Toolsets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Hosting;

// D83: the agents and toolsets the runtime lists, and the hosts it creates by an agent's kind or type
public class RuntimeCatalogTests : AgentTestBase
{
    [Fact]
    public void Given_Registered_Agents_The_Runtime_Lists_Each_With_Its_Name_Kind_And_Description()
    {
        // Act
        var probe = Runtime.Agents.Single(agent => agent.Type == typeof(ProbeAgent));

        // Assert
        probe.Should().Be(new AgentInfo(
            Type: typeof(ProbeAgent),
            Name: "probe_agent",
            Kind: "ProbeAgent",
            Description: "Probes what a run resolves."));
    }

    [Fact]
    public void Given_An_Agent_With_A_Storage_Kind_The_Runtime_Lists_It_Under_That_Kind()
    {
        // Act
        var agent = Runtime.Agents.Single(agent => agent.Type == typeof(EveryKindAgent));

        // Assert
        agent.Kind.Should().Be("every_kind");
    }

    [Fact]
    public void Given_Toolsets_Of_Two_Modules_The_Runtime_Lists_Both()
    {
        // Act
        var types = Runtime.Toolsets.Select(toolset => toolset.Type);

        // Assert
        types.Should().Contain([typeof(ProbeToolset), typeof(CompanyToolset)]);
    }

    [Fact]
    public void Given_A_Toolset_With_A_Display_Name_The_Runtime_Lists_It_With_The_Name()
    {
        // Act
        var companies = Runtime.Toolsets.Single(toolset => toolset.Type == typeof(CompanyToolset));

        // Assert
        companies.DisplayName.Should().Be("Companies");
    }

    [Fact]
    public void Given_A_Toolset_The_Runtime_Lists_Its_Tools_With_Their_Descriptions_And_Marks()
    {
        // Act
        var guarded = Runtime.Toolsets.Single(toolset => toolset.Type == typeof(GuardedToolset));

        // Assert
        guarded.Tools.Should().Equal(
            Tool(GuardedToolset.ToolNames.CloseTicket, "Closes a ticket.", readOnly: false, approval: true),
            Tool(GuardedToolset.ToolNames.ReadTicket, "Reads a ticket.", readOnly: true, approval: false),
            Tool(GuardedToolset.ToolNames.Note, "Notes something.", readOnly: false, approval: false));
    }

    [Fact]
    public async Task Given_An_Agents_Kind_The_Runtime_Creates_Its_Host()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        await using var host = Runtime.CreateHost("ProbeAgent");

        // Act
        var response = await host.RunAsync(Data.Tool.Question, Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_An_Agents_Type_The_Runtime_Creates_Its_Host()
    {
        // Act
        await using var host = Runtime.CreateHost(typeof(ModelOnlyAgent));

        // Assert
        host.Agent.Should().BeOfType<ModelOnlyAgent>();
    }

    [Fact]
    public void Given_A_Kind_No_Agent_Has_Creating_A_Host_Fails_Naming_The_Kinds()
    {
        // Act
        var create = () => Runtime.CreateHost("unknown_kind");

        // Assert
        create.Should()
            .Throw<ArgumentException>()
            .WithMessage("No agent of the kind 'unknown_kind' is registered. The kinds are those of*'ProbeAgent'*");
    }

    [Fact]
    public void Given_A_Type_No_Module_Registered_Creating_A_Host_Fails_Naming_The_Type()
    {
        // Act
        var create = () => Runtime.CreateHost(typeof(Agent));

        // Assert
        create.Should().Throw<ArgumentException>().WithMessage("The agent 'Agent' is not registered.*");
    }

    [Fact]
    public async Task Given_An_Agents_Kind_A_Scope_Resolves_Its_Host_As_A_Keyed_Service_In_That_Scope()
    {
        // Act
        await using var host = Scope.GetRequiredKeyedService<IAgentHost>("ProbeAgent");

        // Assert
        host.Services.Should().BeSameAs(Scope);
    }

    [Fact]
    public async Task Given_A_Host_Of_A_Kind_Its_Copy_With_A_Conversation_Id_Stores_The_Run()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));
        await using var host = Runtime.CreateHost("ProbeAgent");

        // Act
        await host.WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, Ct);

        // Assert
        (await EntriesAsync(Data.Agent.ConversationId)).Should().NotBeEmpty();
    }

    private static ToolInfo Tool(string name, string description, bool readOnly, bool approval) =>
        new(name, description, IsReadOnly: readOnly, RequiresApproval: approval);
}
