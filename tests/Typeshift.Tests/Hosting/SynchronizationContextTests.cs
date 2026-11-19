using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Hosting.Support;
using Typeshift.Tests.Tools.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Hosting;

// A run that starts on a caller's context first waits for its store, and each model update arrives from the pool
public class SynchronizationContextTests : HostTestBase
{
    private readonly CallerContext _context = new();

    private WaitingStore Store => Services.GetRequiredService<WaitingStore>();

    private ContextLog Log => Services.GetRequiredService<ContextLog>();

    private AgentHost<ProbeAgent> Host =>
        Scope.GetRequiredService<AgentHost<ProbeAgent>>().WithConversationId(Data.Agent.ConversationId);

    [Fact]
    public async Task Given_A_Caller_With_A_Synchronization_Context_A_Streaming_Run_Posts_Nothing_To_It()
    {
        // Arrange
        var host = Host;

        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer), Text(Data.Agent.Reason));

        var reading = _context.ReadAsync(() => host.RunStreamingAsync(Data.Tool.Question, ct: Ct));
        await _context.Waiting.WaitAsync(Ct);

        // Act
        Store.Open();
        var updates = await reading.WaitAsync(Ct);

        // Assert
        updates.Should().Be(4);
        _context.Posts.Should().Be(0);
    }

    [Fact]
    public async Task Given_A_Caller_With_A_Synchronization_Context_A_Tool_Runs_Without_It()
    {
        // Arrange
        var host = Host;

        ChatClient
            .Reply(Call(Data.Tool.CallId, "probe", ("query", Data.Tool.Query)))
            .Reply(Text(Data.Tool.Answer));

        var running = _context.RunAsync(() => host.RunAsync(Data.Tool.Question, ct: Ct));
        await _context.Waiting.WaitAsync(Ct);

        // Act
        Store.Open();
        await running.WaitAsync(Ct);

        // Assert
        Log.Calls.Should().Equal(ContextLoggingToolMiddleware.Tool);
        Log.OnContext.Should().BeEmpty();
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddTypeshift()
            .WithStore<WaitingStore>()
            .WithChatClientProvider<NetworkChatClientProvider>()
            .WithToolMiddleware<ContextLoggingToolMiddleware>();

        services.AddSingleton(ChatClient);
        services.AddSingleton<ContextLog>();
        services.AddScoped<CallScope>();
    }
}
