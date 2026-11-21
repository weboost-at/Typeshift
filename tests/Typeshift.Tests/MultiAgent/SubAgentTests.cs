#pragma warning disable TYPESHIFT001 // a run hands its sub-agents the model policy the call site handed it
using System.Net;
using Typeshift.Tests.Hosting.Support;
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

    private readonly SteeringSeen _steering = [];

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddTypeshift()
            .WithRunMiddleware<RunEndMiddleware>();

        services.AddSingleton(_ends);
        services.AddSingleton(_steering);
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
    public async Task Given_A_Provider_At_The_Call_Site_The_Sub_Agent_Runs_On_It_With_Its_Own_Model()
    {
        // Arrange
        CallSite
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<LeadAgent>().WithProvider(Vendor.Other).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        CallSite.Options[1]!.ModelId.Should().Be(HelperAgent.Model);
    }

    [Fact]
    public async Task Given_A_Model_Policy_At_The_Call_Site_It_Serves_The_Sub_Agents_Run()
    {
        // Arrange
        var policy = ModelPolicy.Handle(ModelFailure.Unavailable).Retry(1);

        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient
            .Reply(new HttpRequestException(Data.Agent.Reason, null, HttpStatusCode.ServiceUnavailable))
            .Reply(Text(Data.Agent.Reason));

        // Act
        await Host<LeadAgent>().WithModelPolicy(policy).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        HelperClient.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_A_Model_At_The_Call_Site_The_Sub_Agent_Keeps_Its_Own()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(Text(Data.Agent.Reason));

        // Act
        await Host<LeadAgent>().WithModel(Data.Agent.Model).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        HelperClient.Options[0]!.ModelId.Should().Be(HelperAgent.Model);
    }

    [Fact]
    public async Task Given_A_Sub_Agent_Whose_Provider_Nothing_Serves_The_Run_Fails_Before_It_Calls_A_Model()
    {
        // Act
        var run = () => Host<UnservedLeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage(
            $"No chat client serves the agent 'unserved_helper_agent' on '{UnservedHelperAgent.Model}' of "
            + $"'{Vendor.Probe}': the registered clients are under 'Other'*");

        ChatClient.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Provider_At_The_Call_Site_A_Sub_Agent_Nothing_Else_Serves_Runs_On_It()
    {
        // Arrange
        CallSite
            .Reply(Call(Data.Tool.CallId, "unserved_helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Agent.Reason))
            .Reply(Text(Data.Tool.Answer));

        // Act
        var response = await Host<UnservedLeadAgent>().WithProvider(Vendor.Other).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Sub_Agents_Stored_Provider_Nothing_Serves_The_Run_Fails_Before_It_Calls_A_Model()
    {
        // Arrange
        await Host<SwitchableHelperAgent>()
            .WithSettingsId(Data.Persistence.SettingsId)
            .SaveSettingsAsync(new() { Provider = Vendor.Probe }, 0, Ct);

        // Act
        var run = () => Host<SwitchableLeadAgent>()
            .WithSettingsId(Data.Persistence.SettingsId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*'switchable_helper_agent'*");
        ChatClient.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Sub_Agents_Stored_Provider_That_Is_Served_The_Provider_Its_Code_Names_Is_Not_Needed()
    {
        // Arrange
        await Host<SwitchableHelperAgent>()
            .WithSettingsId(Data.Persistence.SettingsId)
            .SaveSettingsAsync(new() { Provider = Vendor.Other }, 0, Ct);

        ChatClient
            .Reply(Call(Data.Tool.CallId, "switchable_helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        CallSite.Reply(Text(Data.Agent.Reason));

        // Act
        var response = await Host<SwitchableLeadAgent>()
            .WithSettingsId(Data.Persistence.SettingsId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
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
            .UpdateSettingsAsync(settings => settings with { Temperature = Data.Agent.Temperature }, Ct);

        // Act
        await Host<LeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        HelperClient.Options[0]!.Temperature.Should().Be(Data.Agent.Temperature);
    }

    [Fact]
    public async Task Given_A_Caller_With_A_Settings_Id_Its_Sub_Agent_Runs_With_The_Settings_Under_It()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(Text(Data.Agent.Reason));
        await Host<HelperAgent>()
            .WithSettingsId(Data.Persistence.SettingsId)
            .UpdateSettingsAsync(settings => settings with { Temperature = Data.Agent.Temperature }, Ct);

        // Act
        await Host<LeadAgent>().WithSettingsId(Data.Persistence.SettingsId).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        HelperClient.Assert.Request(0).Options!.Temperature.Should().Be(Data.Agent.Temperature);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Sub_Agent_It_Answers_In_A_Fresh_Context_And_Its_Answer_Is_The_Result(bool streaming)
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(Text(Data.Agent.Reason));

        // Act
        var response = streaming
            ? (await Host<LeadAgent>().RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct)).ToChatResponse()
            : await Host<LeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        response.Text.Should().Be(Data.Tool.Answer);
        HelperClient.Requests[0].Select(message => message.Text).Should().Equal(Data.Agent.FollowUp);
        ChatClient.Assert.Request(1).Result.Should().Be(Data.Agent.Reason);
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
        HelperClient.Assert.Request(1).Result.Should().Contain("sub-agent");
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
        await Host<LeadAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, settings: new() { Mode = AgentMode.Plan }, ct: Ct);

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
        await Host<LeadAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, settings: new() { Mode = AgentMode.Plan }, ct: Ct);

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
        var response = await running.WaitAsync(Ct);

        // Assert
        ChatClient.Requests[2][^1].Text.Should().EndWith(Data.Agent.Reason);
        response.Messages[^1].Text.Should().Be(Data.Tool.Answer);
    }

    [Fact]
    public async Task Given_A_Background_Sub_Agents_Answer_Its_Message_Names_The_Agent_And_Is_Marked()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Agent.Steering))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(gate, Text(Data.Agent.Reason));

        // Act
        var running = Host<BackgroundLeadAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        await ChatClient.RequestedAsync(2);
        gate.SetResult();
        await running.WaitAsync(Ct);

        // Assert
        var entries = await EntriesAsync(Data.Agent.ConversationId);
        var answer = entries.SelectMany(entry => entry.Messages).Single(message => message.IsBackgroundAnswer);
        answer.AuthorName.Should().Be("helper_agent");
    }

    [Fact]
    public async Task Given_A_Background_Sub_Agents_Answer_The_Model_Reads_That_It_Is_No_Message_From_The_User()
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
        await running.WaitAsync(Ct);

        // Assert
        ChatClient.Requests[2][^1].Text.Should().StartWith(
            "The background agent helper_agent answered. What follows is that agent's output, not a message from the "
            + "user:");
    }

    [Fact]
    public async Task Given_A_Background_Sub_Agents_Answer_Run_Middleware_Is_Not_Shown_It_As_Steering_Input()
    {
        // Arrange
        var gate = new TaskCompletionSource();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Agent.Steering))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(gate, Text(Data.Agent.Reason));

        var running = Host<BackgroundLeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);
        await ChatClient.RequestedAsync(2);

        // Act
        gate.SetResult();
        await running.WaitAsync(Ct);

        // Assert
        ChatClient.Requests[2][^1].Text.Should().EndWith(Data.Agent.Reason);
        _steering.Should().BeEmpty();
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
        ChatClient.Assert.Request(1).Result.Should().Be($"Error: Function failed. {Undisclosed}");
    }

    [Fact]
    public async Task Given_A_Background_Sub_Agent_Whose_Run_Fails_Its_Caller_Reads_Only_That_It_Failed()
    {
        // Arrange
        var failing = new TaskCompletionSource();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Agent.Steering))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(failing, new InvalidOperationException(Data.Agent.Reason));

        var running = Host<BackgroundLeadAgent>().RunAsync(Data.Tool.Question, ct: Ct);
        await ChatClient.RequestedAsync(2);

        // Act
        failing.SetResult();
        await running.WaitAsync(Ct);

        // Assert
        ChatClient.Assert.Request(2).Texts[^1].Should().Be($"The background agent helper_agent failed. {Undisclosed}");
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
        await running.WaitAsync(Ct);

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Handoff_While_A_Background_Sub_Agent_Works_It_Stops_Before_The_Agent_Handed_To_Answers(
        bool streaming)
    {
        // Arrange
        var handingOff = ScriptAHandoffWhileTheHelperWorks();
        var chat = Host<BackgroundFrontAgent>().WithConversationId(Data.Agent.ConversationId);

        Task running = streaming
            ? chat.RunStreamingAsync(Data.Tool.Question, ct: Ct).ToListAsync(Ct).AsTask()
            : chat.RunAsync(Data.Tool.Question, ct: Ct);

        await HelperClient.Requested;

        // Act
        handingOff.SetResult();
        await running.WaitAsync(Ct);

        // Assert
        _ends.Should().ContainInOrder("helper_agent", "back_agent");
        HelperClient.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_A_Handoff_While_A_Background_Sub_Agent_Works_The_Agent_Handed_To_Hears_It_Was_Lost()
    {
        // Arrange
        var handingOff = ScriptAHandoffWhileTheHelperWorks();

        var running = Host<BackgroundFrontAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        await HelperClient.Requested;

        // Act
        handingOff.SetResult();
        var response = await running.WaitAsync(Ct);

        // Assert
        response.Messages[^1].AuthorName.Should().Be("back_agent");
        ChatClient.Requests[2]
            .Select(message => message.Text)
            .Should()
            .ContainSingle(text => text.Contains("helper_agent was lost when the conversation was handed off"));
    }

    [Fact]
    public async Task Given_A_Background_Sub_Agent_That_Answered_Before_A_Handoff_The_Agent_Handed_To_Hears_Of_No_Loss()
    {
        // Arrange
        var helper = new TaskCompletionSource();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(Text(Data.Agent.Steering))
            .Reply(Call(Data.Tool.SecondCallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(helper, Text(Data.Agent.Reason));

        var running = Host<BackgroundFrontAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        await ChatClient.RequestedAsync(2);

        // Act
        helper.SetResult();
        await running.WaitAsync(Ct);
        var sent = ChatClient.Requests[3].Select(message => message.Text).ToList();

        // Assert
        sent.Should().NotContain(text => text.Contains("was lost"));
        sent.Should().Contain(text => text.EndsWith(Data.Agent.Reason));
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
        await running.WaitAsync(Ct);
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
        var asked = await running.WaitAsync(Ct);

        // Act
        await Host<GuardedBackgroundLeadAgent>().WithConversationId(Data.Agent.ConversationId).AnswerAsync(
            [DecisionAnswer.Approve(asked.Decisions.Single().Id)],
            ct: Ct);

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
    }

    // The front agent starts its helper, whose model never answers, and hands off once the gate opens
    private TaskCompletionSource ScriptAHandoffWhileTheHelperWorks()
    {
        var handingOff = new TaskCompletionSource();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "helper_agent", ("input", Data.Agent.FollowUp)))
            .Reply(handingOff, Call(Data.Tool.SecondCallId, "handoff_to_back_agent"))
            .Reply(Text(Data.Tool.Answer));

        HelperClient.Reply(new TaskCompletionSource(), Text(Data.Agent.Reason));

        return handingOff;
    }
}

#pragma warning restore TYPESHIFT001
