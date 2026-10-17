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

    protected Task<ChatResponse> RunAsync(string input) =>
        Host<ContextAgent>().RunAsync(input, conversationId: Data.Agent.ConversationId, ct: Ct);

    protected async Task<ChatResponse> StreamAsync(string input) =>
        (await Host<ContextAgent>()
            .RunStreamingAsync(input, conversationId: Data.Agent.ConversationId, ct: Ct)
            .ToListAsync(Ct))
        .ToChatResponse();
}

// The guarded tools, with a context provider
public partial class ContextAgent : Agent
{
    public const string Prompt = "Help with tickets.";

    public ContextAgent()
    {
        WithChatOptions().WithModel("scripted", "context-model");

        UseToolset<GuardedToolset>();

        UseContext<ScriptedContext>();
    }

    protected override string GetInstructions() => Prompt;
}
