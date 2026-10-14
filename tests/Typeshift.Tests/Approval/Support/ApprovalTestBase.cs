using Typeshift.Tests.Agents.Support;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Approval.Support;

// The agent tests' host, with two scripted rules and the calls the guarded tools made
public abstract class ApprovalTestBase : AgentTestBase
{
    protected ToolCalls ToolCalls { get; } = [];

    protected RuleScript Rules { get; } = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddTypeshift()
            .WithApprovalRule<ScriptedRule>()
            .WithApprovalRule<SecondScriptedRule>();

        services.AddSingleton(ToolCalls);
        services.AddSingleton(Rules);
    }

    protected Task<ChatResponse> RunAsync(string input, AgentMode? mode = null) =>
        Host<GuardedAgent>().RunAsync(
            input,
            conversationId: Data.Agent.ConversationId,
            settings: mode is null ? null : new() { Mode = mode },
            ct: Ct);

    protected Task<ChatResponse> AnswerAsync(params AIContent[] answers) =>
        Host<GuardedAgent>().RunAsync(
            new ChatMessage(ChatRole.User, [.. answers]),
            conversationId: Data.Agent.ConversationId,
            ct: Ct);

    protected async Task<ChatResponse> StreamAsync(string input) =>
        (await Host<GuardedAgent>()
            .RunStreamingAsync(input, conversationId: Data.Agent.ConversationId, ct: Ct)
            .ToListAsync(Ct))
        .ToChatResponse();

    protected async Task<ChatResponse> StreamAnswerAsync(params AIContent[] answers) =>
        (await Host<GuardedAgent>()
            .RunStreamingAsync(
                new ChatMessage(ChatRole.User, [.. answers]),
                conversationId: Data.Agent.ConversationId,
                ct: Ct)
            .ToListAsync(Ct))
        .ToChatResponse();

    protected ChatResponseUpdate CloseTicket(string callId, int number) =>
        Call(callId, "close_ticket", ("number", number));

    // The one tool result a request carried to the model
    protected string ResultSentToModel(int request) =>
        ChatClient.Requests[request]
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Single()
            .Result?
            .ToString() ?? "";
}
