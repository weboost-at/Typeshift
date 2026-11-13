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

    protected async Task<ChatResponse> StreamAsync(string input) =>
        (await Host<ContextAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(input, ct: Ct)
            .ToListAsync(Ct))
        .ToChatResponse();
}

// The guarded tools, with a context provider, compacting once a test's run settings turn it on
public partial class ContextAgent : Agent
{
    public const string Prompt = "Help with tickets.";

    public ContextAgent()
    {
        WithChatOptions().WithModel("scripted", "context-model");

        WithRunOptions().Configurable(r => r.MaxInputTokens);

        UseToolset<GuardedToolset>();

        UseContext<ScriptedContext>();
    }

    protected override string GetInstructions() => Prompt;
}
