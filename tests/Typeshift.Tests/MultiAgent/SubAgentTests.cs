using Typeshift.Tests.MultiAgent.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.MultiAgent;

public class SubAgentTests : MultiAgentTestBase
{
    // What a model reads of a failure nobody wrote for it
    private const string Undisclosed = "The details of the failure are not available to you.";

    private readonly RunEnds _ends = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddTypeshift()
            .WithRunMiddleware<RunEndMiddleware>();

        services.AddSingleton(_ends);
    }

    [Fact]
    public async Task Given_A_Sub_Agent_It_Is_Offered_With_Its_Description_And_One_Input()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<LeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        var tool = ChatClient.Options[0]!.Tools!.OfType<AIFunctionDeclaration>().Single();
        tool.Name.Should().Be("helper_agent");
        tool.Description.Should().Be(HelperAgent.About);
        tool.JsonSchema.GetProperty("required")
            .EnumerateArray()
            .Select(name => name.GetString())
            .Should()
            .Equal("input");
    }

    [Fact]
    public async Task Given_The_Sub_Agents_Own_Settings_Its_Run_Applies_Them()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(Text(Data.Agent.Reason));
        await Host<HelperAgent>()
            .ConfigureAsync(settings => settings with { Temperature = Data.Agent.Temperature }, Ct);

        // Act
        await Host<LeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        HelperClient.Options[0]!.Temperature.Should().Be(Data.Agent.Temperature);
    }

    [Fact]
    public async Task Given_A_Sub_Agent_It_Answers_In_A_Fresh_Context_And_Its_Answer_Is_The_Result()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(Text(Data.Agent.Reason));

        // Act
        var response = await Host<LeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
        HelperClient.Requests[0].Select(message => message.Text).Should().Equal(Data.Agent.FollowUp);
        Result(ChatClient.Requests[1]).Should().Be(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Sub_Agent_In_The_Default_Mode_A_Call_That_Needs_Approval_Is_Rejected()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient
            .Reply(Call(Data.Tool.SecondCallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Text(Data.Agent.Reason));

        // Act
        await Host<LeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ToolCalls.Should().BeEmpty();
        Result(HelperClient.Requests[1]).Should().Contain("sub-agent");
    }

    [Fact]
    public async Task Given_A_Tool_Approved_For_The_Callers_Conversation_The_Sub_Agents_Call_Of_It_Runs()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Call(Data.Tool.SecondCallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient
            .Reply(Call($"{Data.Tool.CallId}_helper", "close_ticket", ("number", Data.Agent.Ticket.Updated)))
            .Reply(Text(Data.Agent.Reason));

        var lead = Host<TrustingLeadAgent>();
        var asked = await lead.WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);
        var always = DecisionAnswer.ApproveForConversation(asked.Decisions[0].Id);

        // Act
        await lead.WithConversationId(Data.Agent.ConversationId).AnswerAsync([always], ct: Ct);

        // Assert
        ToolCalls.Should().Equal(
            $"close_ticket {Data.Agent.Ticket.Default}",
            $"close_ticket {Data.Agent.Ticket.Updated}");
    }

    [Fact]
    public async Task Given_A_Caller_In_Auto_Mode_Its_Sub_Agent_Runs_In_Auto_Mode()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient
            .Reply(Call(Data.Tool.SecondCallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Text(Data.Agent.Reason));

        // Act
        await Host<LeadAgent>().RunAsync(Data.Tool.Question, settings: new() { Mode = AgentMode.Auto }, ct: Ct);

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    [Fact]
    public async Task Given_A_Tool_Of_The_Chat_Options_Named_Like_A_Sub_Agent_The_Run_Fails_Naming_Both()
    {
        // Act
        var run = () => Host<ShadowedLeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*'helper_agent' twice, through its sub-agents and through its chat options or a context*");
        ChatClient.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Caller_In_Plan_Mode_Its_Sub_Agents_Are_Offered()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<LeadAgent>().RunAsync(Data.Tool.Question, settings: new() { Mode = AgentMode.Plan }, ct: Ct);

        // Assert
        ChatClient.Options[0]!.Tools!.Select(tool => tool.Name).Should().Equal("helper_agent", "exit_plan_mode");
    }

    [Fact]
    public async Task Given_A_Caller_In_Plan_Mode_Its_Sub_Agent_Is_Offered_Only_Its_Read_Only_Tools()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(Text(Data.Agent.Reason));

        // Act
        await Host<LeadAgent>().RunAsync(Data.Tool.Question, settings: new() { Mode = AgentMode.Plan }, ct: Ct);

        // Assert
        HelperClient.Options[0]!.Tools!.Select(tool => tool.Name).Should().Equal("read_ticket");
    }

    [Fact]
    public async Task Given_A_Background_Sub_Agent_The_Caller_Works_On_And_Receives_Its_Answer_Before_It_Ends()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Agent.Steering))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(gate, Text(Data.Agent.Reason));

        // Act
        var running = Host<BackgroundLeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);
        await ChatClient.RequestedAsync(2);
        gate.SetResult();
        var response = await running;

        // Assert
        ChatClient.Requests[2][^1].Text.Should().EndWith(Data.Agent.Reason);
        response.Messages[^1].Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Sub_Agent_Whose_Run_Fails_Its_Caller_Reads_Only_That_The_Call_Failed()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(new InvalidOperationException(Data.Agent.Reason));

        // Act
        await Host<LeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        Result(ChatClient.Requests[1]).Should().Be($"Error: Function failed. {Undisclosed}");
    }

    [Fact]
    public async Task Given_A_Background_Sub_Agent_Whose_Run_Fails_Its_Caller_Reads_Only_That_It_Failed()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Agent.Steering))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(new InvalidOperationException(Data.Agent.Reason));

        // Act
        await Host<BackgroundLeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        ChatClient.Requests[^1][^1].Text.Should().Be($"The background agent helper_agent failed. {Undisclosed}");
    }

    [Fact]
    public async Task Given_A_Background_Sub_Agent_That_Answered_The_Next_Run_Hears_Of_No_Loss()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Agent.Steering))
            .Reply(Text(Data.Tool.Answer))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(gate, Text(Data.Agent.Reason));

        var chat = Host<BackgroundLeadAgent>().WithConversationId(Data.Agent.ConversationId);
        var running = chat.RunAsync(Data.Tool.Question, ct: Ct);
        await ChatClient.RequestedAsync(2);
        gate.SetResult();
        await running;

        // Act
        await chat.RunAsync(Data.Agent.FollowUp, ct: Ct);
        var sent = ChatClient.Requests[^1].Select(message => message.Text).ToList();

        // Assert
        sent.Should().NotContain(text => text.Contains("was lost"));
        sent.Should().Contain(text => text.EndsWith(Data.Agent.Reason));
    }

    [Fact]
    public async Task Given_An_Interrupted_Run_With_A_Background_Sub_Agent_The_Model_Hears_It_Was_Lost()
    {
        // Arrange
        var store = Services.GetRequiredService<ITypeshiftStore<Guid>>();
        var conversation = Data.Agent.ConversationId;

        ChatMessage[] steps =
        [
            new(ChatRole.User, Data.Tool.Question),
            new(ChatRole.Assistant, [new FunctionCallContent(Data.Tool.CallId, "helper_agent")]),
            new(ChatRole.Tool, [new FunctionResultContent(Data.Tool.CallId, "started")])
        ];

        await store.AppendConversationAsync(conversation, new("background_lead_agent", steps, Time.GetUtcNow()), Ct);
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<BackgroundLeadAgent>().WithConversationId(conversation).ResumeAsync(ct: Ct);

        // Assert
        ChatClient.Requests[0][^1].Text.Should().Contain("helper_agent was lost");
    }

    [Fact]
    public async Task Given_A_Cancelled_Run_With_A_Background_Sub_Agent_The_Next_Run_Hears_It_Was_Lost()
    {
        // Arrange
        var store = Services.GetRequiredService<ITypeshiftStore<Guid>>();
        var conversation = Data.Agent.ConversationId;

        ChatMessage[] steps =
        [
            new(ChatRole.User, Data.Tool.Question),
            new(ChatRole.Assistant, [new FunctionCallContent(Data.Tool.CallId, "helper_agent")]),
            new(ChatRole.Tool, [new FunctionResultContent(Data.Tool.CallId, "started")])
        ];

        await store.AppendConversationAsync(conversation, new("background_lead_agent", steps, Time.GetUtcNow()), Ct);
        var end = new ConversationEntry("background_lead_agent", [], Time.GetUtcNow()) { EndsRun = true };

        await store.AppendConversationAsync(conversation, end, Ct);
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<BackgroundLeadAgent>().WithConversationId(conversation).RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        ChatClient.Requests[0].Select(message => message.Text).Should().Contain(text => text.Contains("was lost"));
    }

    [Fact]
    public async Task Given_A_Run_Cancelled_While_A_Background_Sub_Agent_Works_It_Ends_After_The_Sub_Agent()
    {
        // Arrange
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Agent.Steering));

        HelperClient.Reply(new TaskCompletionSource(), Text(Data.Agent.Reason));

        var running = Host<BackgroundLeadAgent>().RunAsync(Data.Tool.Question, ct: cancel.Token);
        await ChatClient.RequestedAsync(2);
        await HelperClient.Requested;

        // Act
        await cancel.CancelAsync();
        await Task.WhenAny(running).WaitAsync(Ct);

        // Assert
        running.IsCanceled.Should().BeTrue();
        _ends.Should().Contain("helper_agent");
    }

    [Fact]
    public async Task Given_A_Compaction_While_A_Background_Sub_Agent_Works_Its_Summary_Keeps_The_Call()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Agent.Steering))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(gate, Text(Data.Agent.Reason));

        // Act
        var running = Host<BackgroundLeadAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(
            new string('x', Page),
            settings: new() { MaxInputTokens = 1_000 },
            ct: Ct);

        await ChatClient.RequestedAsync(3);
        gate.SetResult();
        await running;
        var summary = (await EntriesAsync(Data.Agent.ConversationId)).Single(entry => entry.IsSummary);

        // Assert
        summary.BackgroundCalls.Should().Contain(Data.Tool.CallId, "helper_agent");
    }

    [Fact]
    public async Task Given_A_Summary_That_Left_A_Background_Sub_Agent_Working_The_Next_Run_Hears_It_Was_Lost()
    {
        // Arrange
        var store = Services.GetRequiredService<ITypeshiftStore<Guid>>();
        var conversation = Data.Agent.ConversationId;

        var summary = new ConversationEntry(
            "background_lead_agent",
            [new ChatMessage(ChatRole.User, Data.Agent.Reason)],
            Time.GetUtcNow())
        {
            IsSummary = true,
            BackgroundCalls = new Dictionary<string, string> { [Data.Tool.CallId] = "helper_agent" }
        };

        await store.AppendConversationAsync(conversation, summary, Ct);
        var end = new ConversationEntry("background_lead_agent", [], Time.GetUtcNow()) { EndsRun = true };

        await store.AppendConversationAsync(conversation, end, Ct);
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<BackgroundLeadAgent>().WithConversationId(conversation).RunAsync(Data.Agent.FollowUp, ct: Ct);

        // Assert
        ChatClient.Requests[0]
            .Select(message => message.Text)
            .Should()
            .Contain(text => text.Contains("helper_agent was lost"));
    }

    [Fact]
    public async Task Given_A_Decision_While_A_Background_Sub_Agent_Works_The_Run_Returns_It_And_Its_Approval_Runs()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Call(Data.Tool.SecondCallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(gate, Text(Data.Agent.Reason));

        var running = Host<GuardedBackgroundLeadAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        await ChatClient.RequestedAsync(2);
        gate.SetResult();
        var asked = await running;

        // Act
        await Host<GuardedBackgroundLeadAgent>().WithConversationId(Data.Agent.ConversationId).AnswerAsync(
            [DecisionAnswer.Approve(asked.Decisions.Single().Id)],
            ct: Ct);

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    private static string? Result(IEnumerable<ChatMessage> request) =>
        request.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Single().Result?.ToString();
}
