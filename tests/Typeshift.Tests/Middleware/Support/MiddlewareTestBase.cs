using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Approval.Support;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Middleware.Support;

// The agent tests' host, with two run middleware, two tool middleware and an agent that uses a context provider
public abstract class MiddlewareTestBase : AgentTestBase
{
    protected MiddlewareScript Script { get; } = new();

    protected ToolCalls ToolCalls { get; } = [];

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddTypeshift()
            .WithRunMiddleware<FirstRunMiddleware>()
            .WithRunMiddleware<SecondRunMiddleware>()
            .WithToolMiddleware<FirstToolMiddleware>()
            .WithToolMiddleware<SecondToolMiddleware>();

        services.AddSingleton(Script);
        services.AddSingleton(ToolCalls);
    }

    protected Task<ChatResponse> RunAsync(string input, ContextAgent.Settings? settings = null) =>
        Host<ContextAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(input, settings: settings, ct: Ct);

    protected async Task<ChatResponse> StreamAsync(string input, ContextAgent.Settings? settings = null) =>
        (await Host<ContextAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(input, settings: settings, ct: Ct)
            .ToListAsync(Ct))
        .ToChatResponse();

    // The same run on either path, a streamed one as the response its updates make
    protected Task<ChatResponse> RunAsync(string input, ContextAgent.Settings? settings, bool streaming) =>
        streaming ? StreamAsync(input, settings) : RunAsync(input, settings);
}

// The guarded tools, with a context provider, compacting once a test's run settings turn it on
public partial class ContextAgent : Agent
{
    public const string Prompt = "Help with tickets.";

    public ContextAgent()
    {
        WithInstructions(Prompt);

        WithChatOptions().WithModel("scripted", "context-model");

        WithRunOptions()
            .Configurable(r => r.MaxInputTokens)
            .Configurable(r => r.TargetInputTokens);

        UseToolset<GuardedToolset>();

        UseContext<ScriptedContext>();
    }
}
