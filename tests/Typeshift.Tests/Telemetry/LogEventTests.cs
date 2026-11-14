using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Approval.Support;
using Typeshift.Tests.Compaction.Support;
using Typeshift.Tests.Conversations.Support;
using Typeshift.Tests.MultiAgent.Support;
using Typeshift.Tests.Testing.Fakes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Typeshift.Tests.Telemetry;

// D36: the events Typeshift logs itself, each under the category Typeshift with an id and a name of its own
public class LogEventTests : MultiAgentTestBase
{
    private const string Category = "Typeshift";

    private readonly EventLog _log = new();

    private readonly RuleScript _rules = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddTypeshift()
            .WithApprovalRule<ScriptedRule>()
            .WithRealtimeTracking(new RealtimeTrackingOptions { MaxPendingUpdates = 1 });

        services.AddSingleton<ILoggerFactory>(_log);
        services.AddSingleton(_rules);
    }

    [Fact]
    public async Task Given_A_Run_The_Model_It_Resolved_Is_Logged_Once()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        _log.Of("RunResolved").Select(logged => logged.Message).Should().Equal(
            $"Agent probe_agent runs on model {ProbeAgent.Model} of provider {ProbeAgent.ModelProvider}, served by "
            + "ScriptedChatClient, with the settings of its code.");
    }

    [Fact]
    public async Task Given_A_Run_The_Model_It_Resolved_Is_A_Debug_Event_Of_The_Category_Typeshift()
    {
        // Arrange
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await Host<ProbeAgent>().RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        _log.Of("RunResolved").Should().ContainSingle(logged =>
            logged.Id.Id == 10 && logged.Level == LogLevel.Debug && logged.Category == Category);
    }

    [Fact]
    public async Task Given_Stored_And_Run_Settings_The_Resolved_Run_Names_Each_Layer_In_The_Order_It_Applied()
    {
        // Arrange
        var host = Host<ProbeAgent>();
        var customer = host.WithSettingsId(Data.Persistence.SettingsId);
        await host.ConfigureAsync(settings => settings with { Temperature = Data.Agent.Temperature }, Ct);
        await customer.ConfigureAsync(settings => settings with { Model = Data.Agent.Model }, Ct);
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await customer.RunAsync(Data.Tool.Question, settings: new() { Instructions = Data.Agent.Instructions }, ct: Ct);

        // Assert
        _log.Of("RunResolved").Single().Message.Should().EndWith(
            $"with the settings of its code, its stored settings, settings id {Data.Persistence.SettingsId}, the run.");
    }

    [Fact]
    public async Task Given_A_Background_Sub_Agent_That_Fails_The_Failure_Is_Logged_With_Its_Exception()
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
        _log.Of("BackgroundAgentFailed").Should().ContainSingle(logged =>
            logged.Id.Id == 6
            && logged.Level == LogLevel.Error
            && logged.Category == Category
            && logged.Exception!.Message == Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Watcher_That_Falls_Behind_The_Updates_It_Loses_Are_Logged_Once()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Call(Data.Tool.SecondCallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var watch = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).GetAsyncEnumerator(Ct);
        var first = watch.MoveNextAsync();

        // Act
        await Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);
        await first;

        // Assert
        _log.Of("UpdatesDropped").Select(logged => logged.Message).Should().Equal(
            $"A watcher of conversation {Data.Agent.ConversationId} lost its oldest updates, since more than 1 "
            + "waited for it: read the feed faster, or raise MaxPendingUpdates with WithRealtimeTracking.");
    }

    [Fact]
    public async Task Given_A_Watcher_That_Falls_Behind_The_Loss_Is_A_Warning_Of_The_Category_Typeshift()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        using var watching = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var watch = Tracker.WatchAsync(Data.Agent.ConversationId, watching.Token).GetAsyncEnumerator(Ct);
        var first = watch.MoveNextAsync();

        // Act
        await Host<ProbeAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);
        await first;

        // Assert
        _log.Of("UpdatesDropped").Should().ContainSingle(logged =>
            logged.Id.Id == 9 && logged.Level == LogLevel.Warning && logged.Category == Category);
    }

    [Fact]
    public async Task Given_A_Rule_That_Approves_Its_Verdict_Is_Logged_With_The_Rule_And_The_Call()
    {
        // Arrange
        _rules.Verdicts.Add(_ => RuleVerdict.Approve);

        ChatClient
            .Reply(Call(Data.Tool.CallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<GuardedAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        _log.Of("RuleDecided").Select(logged => logged.Message).Should().Equal(
            $"The approval rule ScriptedRule approved the call {Data.Tool.CallId} of close_ticket.");
    }

    [Fact]
    public async Task Given_A_Rule_That_Rejects_Its_Verdict_Is_A_Debug_Event_Of_The_Category_Typeshift()
    {
        // Arrange
        _rules.Verdicts.Add(_ => RuleVerdict.Reject(Data.Agent.Reason));

        ChatClient
            .Reply(Call(Data.Tool.CallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<GuardedAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        _log.Of("RuleDecided").Should().ContainSingle(logged =>
            logged.Id.Id == 12
            && logged.Level == LogLevel.Debug
            && logged.Category == Category
            && logged.Message.Contains("ScriptedRule rejected"));
    }

    [Fact]
    public async Task Given_A_Rule_That_Abstains_Nothing_Is_Logged_For_It()
    {
        // Arrange
        ChatClient.Reply(Call(Data.Tool.CallId, "close_ticket", ("number", Data.Agent.Ticket.Default)));

        // Act
        await Host<GuardedAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        _log.Of("RuleDecided").Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Input_Over_The_Maximum_The_Compaction_Is_Logged_With_The_Messages_It_Took_And_Left()
    {
        // Arrange
        ChatClient
            .Reply(Call(Data.Tool.CallId, "read", ("length", Page)))
            .Reply(Call(Data.Tool.SecondCallId, "read", ("length", Page)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await Host<CompactingAgent>().RunAsync(
            Data.Tool.Question,
            settings: new() { MaxInputTokens = 2_000, TargetInputTokens = 1_500 },
            ct: Ct);

        // Assert
        _log.Of("Compacted").Should().ContainSingle(logged =>
            logged.Id.Id == 11
            && logged.Level == LogLevel.Debug
            && logged.Category == Category
            && logged.Message.StartsWith("Agent compacting_agent compacted a call of 5 messages from about ")
            && logged.Message.Contains(" tokens to 5 messages of about "));
    }

    [Fact]
    public async Task Given_A_Store_That_Fails_To_Append_Each_Failure_Is_An_Error_Of_The_Category_Typeshift()
    {
        // Arrange
        var services = new ServiceCollection();
#pragma warning disable TSH036 // the store fails every append, and the run never asks
        services.AddTypeshift().WithStore<FailingAppendStore>().WithChatClientProvider<ScriptedChatClientProvider>();
#pragma warning restore TSH036
        services.AddSingleton(ChatClient).AddSingleton(Models).AddSingleton<ILoggerFactory>(_log);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        _log.Of("StepNotStored").Should().Contain(logged =>
            logged.Id.Id == 2
            && logged.Level == LogLevel.Error
            && logged.Category == Category
            && logged.Exception is IOException);
    }

    [Fact]
    public async Task Given_No_Store_The_First_Write_Is_A_Warning_Of_The_Category_Typeshift()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithChatClientProvider<ScriptedChatClientProvider>();
        services.AddSingleton(ChatClient).AddSingleton(Models).AddSingleton<ILoggerFactory>(_log);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        ChatClient.Reply(Text(Data.Tool.Answer));

        // Act
        await scope.ServiceProvider.GetRequiredService<AgentHost<ProbeAgent>>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunAsync(Data.Tool.Question, ct: Ct);

        // Assert
        _log.Of("NoStore").Should().ContainSingle(logged =>
            logged.Id.Id == 1 && logged.Level == LogLevel.Warning && logged.Category == Category);
    }
}
