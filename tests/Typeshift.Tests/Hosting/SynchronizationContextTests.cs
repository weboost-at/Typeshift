using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Approval.Support;
using Typeshift.Tests.Hosting.Support;
using Typeshift.Tests.Tools.Support;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Hosting;

// Runs started on a caller's context, on the store in memory, with each model update arriving from the pool
public class SynchronizationContextTests : HostTestBase
{
    private readonly CallerContext _context = new();

    // A run middleware reads the model's stream ahead of the caller. Without one, the caller's own calls drive the
    // stream, and leaving it stops the run where it is
    private bool _withRunMiddleware = true;

    private ContextLog Log => Services.GetRequiredService<ContextLog>();

    private ToolCalls ToolCalls => Services.GetRequiredService<ToolCalls>();

    [Fact]
    public async Task Given_A_Caller_With_A_Synchronization_Context_A_Streaming_Run_Posts_Nothing_To_It()
    {
        // Arrange
        var host = Host<ProbeAgent>();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer), Text(Data.Agent.Reason));

        // Act
        var updates = await _context.ReadAsync(() => host.RunStreamingAsync(Data.Tool.Question, ct: Ct));

        // Assert
        updates.Should().Be(4);
        _context.Posts.Should().Be(0);
        Log.OnContext.Should().BeEmpty();
        Log.Calls.Should().Contain([ContextLog.RunMiddleware, ContextLog.Tool, ContextLog.Append]);
    }

    [Fact]
    public async Task Given_A_Caller_With_A_Synchronization_Context_Its_Async_Locals_Reach_A_Streaming_Runs_Tools()
    {
        // Arrange
        var host = Host<ProbeAgent>();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await _context.ReadAsync(() =>
        {
            Log.Flowing.Value = Data.Agent.Reason;
            return host.RunStreamingAsync(Data.Tool.Question, ct: Ct);
        });

        // Assert
        Log.FlowedTo(ContextLog.Tool).Should().Equal(Data.Agent.Reason);
    }

    [Fact]
    public async Task Given_A_Caller_That_Stays_On_Its_Context_A_Streaming_Run_Calls_The_App_Without_It()
    {
        // Arrange
        _withRunMiddleware = false;
        var host = Host<ProbeAgent>();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer), Text(Data.Agent.Reason));

        // Act
        await _context.StayAndReadAsync(() => host.RunStreamingAsync(Data.Tool.Question, ct: Ct), int.MaxValue);

        // Assert
        Log.OnContext.Should().BeEmpty();
        Log.Calls.Should().Contain([ContextLog.Tool, ContextLog.Append]);
    }

    [Fact]
    public async Task Given_A_Caller_That_Stays_On_Its_Context_And_Leaves_A_Stream_The_Run_Stops_Without_It()
    {
        // Arrange
        _withRunMiddleware = false;
        var host = Host<ProbeAgent>();

        ChatClient.Reply(Text(Data.Tool.Answer), Text(Data.Agent.Reason));

        // Act
        await _context.StayAndReadAsync(() => host.RunStreamingAsync(Data.Tool.Question, ct: Ct), 1);

        // Assert
        Log.Calls.Should().EndWith(ContextLog.Append);
        Log.OnContext.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Caller_With_A_Synchronization_Context_A_Run_Calls_The_App_Without_It()
    {
        // Arrange
        var host = Host<ProbeAgent>();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await _context.RunAsync(() => host.RunAsync(Data.Tool.Question, ct: Ct));

        // Assert
        _context.Posts.Should().Be(0);
        Log.OnContext.Should().BeEmpty();
        Log.Calls.Should().Contain([ContextLog.RunMiddleware, ContextLog.Tool, ContextLog.Append]);
    }

    [Fact]
    public async Task Given_A_Caller_With_A_Synchronization_Context_An_Answer_Carries_The_Call_Out_Without_It()
    {
        // Arrange
        var host = Host<GuardedAgent>();

        ChatClient
            .Reply(Call(Data.Tool.CallId, "close_ticket", ("number", Data.Agent.Ticket.Default)))
            .Reply(Text(Data.Tool.Answer));

        var asked = await host.RunAsync(Data.Tool.Question, ct: Ct);
        var approved = DecisionAnswer.Approve(asked.Decisions[0].Id);

        // Act
        await _context.RunAsync(() => host.AnswerAsync([approved], ct: Ct));

        // Assert
        ToolCalls.Should().Equal($"close_ticket {Data.Agent.Ticket.Default}");
        _context.Posts.Should().Be(0);
        Log.OnContext.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Caller_With_A_Synchronization_Context_The_Raw_Client_Streams_Without_It()
    {
        // Arrange
        using var client = await Host<ProbeAgent>().CreateChatClientAsync(Ct);
        ChatMessage[] question = [new(ChatRole.User, Data.Tool.Question)];

        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        // Act
        await _context.ReadAsync(() => client.GetStreamingResponseAsync(question, cancellationToken: Ct));

        // Assert
        _context.Posts.Should().Be(0);
        Log.OnContext.Should().BeEmpty();
        Log.Calls.Should().Contain([ContextLog.RunMiddleware, ContextLog.Tool, ContextLog.Append]);
    }

    [Fact]
    public async Task Given_A_Caller_With_A_Synchronization_Context_A_Settings_Update_Calls_The_Store_Without_It()
    {
        // Arrange
        var host = Scope.GetRequiredService<AgentHost<ProbeAgent>>().WithSettingsId(Data.Agent.ConversationId);

        // Act
        await _context.RunAsync(() =>
            host.UpdateSettingsAsync(settings => settings with { Temperature = Data.Agent.Temperature }, Ct));

        // Assert
        Log.Calls.Should().Equal(ContextLog.Load, ContextLog.Save);
        Log.OnContext.Should().BeEmpty();
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        var typeshift = services.AddTypeshift()
            .WithStore<ContextLoggingStore>()
            .WithClientOfEveryProvider((_, _) => new NetworkChatClient(ChatClient))
            .WithToolMiddleware<ContextLoggingToolMiddleware>();

        if (_withRunMiddleware) typeshift.WithRunMiddleware<ContextLoggingRunMiddleware>();

        services.AddSingleton(ChatClient);
        services.AddSingleton<ContextLog>();
        services.AddSingleton<ToolCalls>();
        services.AddScoped<CallScope>();
    }

    private AgentHost<TAgent> Host<TAgent>() where TAgent : Agent =>
        Scope.GetRequiredService<AgentHost<TAgent>>().WithConversationId(Data.Agent.ConversationId);
}
