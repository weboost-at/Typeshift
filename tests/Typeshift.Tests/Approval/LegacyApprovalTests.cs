using Typeshift.Tests.Approval.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Approval;

// Conversations the version before the decisions document stored: they load, no provider receives their approval
// contents, and nothing they left open or granted is carried over
public class LegacyApprovalTests : ApprovalTestBase
{
    [Fact]
    public async Task Given_A_Request_An_Earlier_Version_Left_Open_The_Next_Run_Sends_Neither_It_Nor_Its_Call()
    {
        // Arrange
        await SeedAsync(LeftOpen());
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Agent.FollowUp);

        // Assert
        SentToModel(0).Should().AllBeOfType<TextContent>();
        ChatClient.Requests[0].Select(message => message.Text).Should().Equal(Data.Tool.Question, Data.Agent.FollowUp);
    }

    [Fact]
    public async Task Given_A_Request_An_Earlier_Version_Left_Open_Nothing_Waits_For_Your_App()
    {
        // Arrange
        await SeedAsync(LeftOpen());

        // Act
        var decisions = await DecisionsAsync();

        // Assert
        decisions.Open.Should().BeEmpty();
        decisions.CanResume.Should().BeFalse();
    }

    [Fact]
    public async Task Given_An_Answer_An_Earlier_Version_Stored_And_Never_Carried_Out_The_Next_Run_Runs_Nothing()
    {
        // Arrange
        await SeedAsync([.. LeftOpen(), LegacyEntries.Approval(Data.Tool.CallId, Data.Agent.Ticket)]);
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Agent.FollowUp);

        // Assert
        ToolCalls.Should().BeEmpty();
        SentToModel(0).Should().AllBeOfType<TextContent>();
    }

    [Fact]
    public async Task Given_A_Call_An_Earlier_Version_Carried_Out_The_Next_Run_Sends_It_Once_With_Its_Result()
    {
        // Arrange
        await SeedAsync(CarriedOut());
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Agent.FollowUp);

        // Assert
        SentToModel(0).Select(content => content.GetType())
            .Should()
            .Equal(
                typeof(TextContent),
                typeof(FunctionCallContent),
                typeof(FunctionResultContent),
                typeof(TextContent),
                typeof(TextContent));
    }

    [Fact]
    public async Task Given_An_Approval_For_The_Conversation_An_Earlier_Version_Stored_The_Tool_Asks_Again()
    {
        // Arrange
        await SeedAsync(CarriedOut());
        ChatClient.Reply(CloseTicket(Data.Tool.SecondCallId, Data.Agent.Ticket.Updated));

        // Act
        var response = await RunAsync(Data.Agent.FollowUp);

        // Assert
        response.Decisions.Should().ContainSingle();
        ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Summary_With_Standing_Approvals_An_Earlier_Version_Stored_The_Tool_Asks_Again()
    {
        // Arrange
        await SeedAsync(LegacyEntries.Summary(Data.Agent.Reason, AgentMode.Default, standing: "close_ticket"));
        ChatClient.Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket));

        // Act
        var response = await RunAsync(Data.Agent.FollowUp);

        // Assert
        response.Decisions.Should().ContainSingle();
        ChatClient.Requests[0][0].Text.Should().Be(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Summary_In_Plan_Mode_An_Earlier_Version_Stored_The_Next_Run_Plans()
    {
        // Arrange
        await SeedAsync(LegacyEntries.Summary(Data.Agent.Reason, AgentMode.Plan, standing: "close_ticket"));
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Agent.FollowUp);

        // Assert
        ChatClient.Options[0]!.Tools!.Select(tool => tool.Name).Should().Equal("read_ticket", "exit_plan_mode");
    }

    [Fact]
    public async Task Given_A_Run_An_Earlier_Version_Ended_In_Auto_Mode_The_Next_Runs_Call_Does_Not_Ask()
    {
        // Arrange
        await SeedAsync(Ended(Data.Tool.Question, AgentMode.Auto));

        ChatClient
            .Reply(CloseTicket(Data.Tool.CallId, Data.Agent.Ticket))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await RunAsync(Data.Agent.FollowUp);

        // Assert
        response.Decisions.Should().BeEmpty();
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_A_Mode_An_Earlier_Version_Stored_A_Run_That_Asks_Copies_It_To_The_Decisions()
    {
        // Arrange
        await SeedAsync(Ended(Data.Tool.Question, AgentMode.Plan));
        ChatClient.Reply(Call(Data.Tool.CallId, "exit_plan_mode", ("plan", Data.Tool.Answer)));

        // Act
        await RunAsync(Data.Agent.FollowUp);
        var decisions = await DecisionsAsync();

        // Assert
        decisions.Mode.Should().Be(AgentMode.Plan);
    }

    [Fact]
    public async Task Given_A_Mode_An_Earlier_Version_Stored_A_Run_That_Asks_Nothing_Stores_No_Decisions()
    {
        // Arrange
        await SeedAsync(Ended(Data.Tool.Question, AgentMode.Plan));
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await RunAsync(Data.Agent.FollowUp);

        // Assert
        (await StoredDecisionsAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Given_A_Mode_An_Earlier_Version_Stored_A_New_Summary_Keeps_It_For_The_Runs_After()
    {
        // Arrange
        await SeedAsync(Ended(new string('x', Page), AgentMode.Plan));

        ChatClient
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        await Chat.RunAsync(Data.Agent.FollowUp, settings: new() { MaxInputTokens = 1_000 }, ct: Ct);

        // Act
        await RunAsync(Data.Agent.Steering);

        // Assert
        (await EntriesAsync(Data.Agent.ConversationId)).Should().Contain(entry => entry.IsSummary);
        ChatClient.Options[^1]!.Tools!.Select(tool => tool.Name).Should().Equal("read_ticket", "exit_plan_mode");
    }

    [Fact]
    public async Task Given_A_Mode_An_Earlier_Version_Stored_Setting_The_Mode_Replaces_It()
    {
        // Arrange
        await SeedAsync(Ended(Data.Tool.Question, AgentMode.Plan));
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Chat.SetModeAsync(AgentMode.Default, Ct);
        await RunAsync(Data.Agent.FollowUp);

        // Assert
        ChatClient.Options[0]!.Tools!.Select(tool => tool.Name).Should().Equal("close_ticket", "read_ticket", "note");
    }

    // A run that answered and ended in a mode, which its last entry names
    private string[] Ended(string input, AgentMode mode) =>
    [
        LegacyEntries.Input(input),
        LegacyEntries.Answer(Data.Tool.Answer),
        LegacyEntries.End(mode)
    ];

    // A run that asked for approval and ended, in the default mode
    private string[] LeftOpen() =>
    [
        LegacyEntries.Input(Data.Tool.Question),
        LegacyEntries.Calling(Data.Tool.CallId, Data.Agent.Ticket),
        LegacyEntries.Request(Data.Tool.CallId, Data.Agent.Ticket),
        LegacyEntries.End(AgentMode.Default)
    ];

    // The answer for the conversation and the run it started, which stored the approved call a second time
    private string[] CarriedOut() =>
    [
        .. LeftOpen(),
        LegacyEntries.Approval(Data.Tool.CallId, Data.Agent.Ticket, forConversation: true),
        LegacyEntries.Calling(Data.Tool.CallId, Data.Agent.Ticket),
        LegacyEntries.Result(Data.Tool.CallId, Data.Agent.Ticket),
        LegacyEntries.Answer(Data.Tool.Answer),
        LegacyEntries.End(AgentMode.Default)
    ];

    private async Task SeedAsync(params string[] entries)
    {
        var store = Services.GetRequiredService<ITypeshiftStore<Guid>>();

        foreach (var entry in entries)
        {
            await store.AppendConversationAsync(Data.Agent.ConversationId, ConversationEntry.Deserialize(entry), Ct);
        }
    }

    private IEnumerable<AIContent> SentToModel(int request) =>
        ChatClient.Requests[request].SelectMany(message => message.Contents);
}
