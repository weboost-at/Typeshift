using Typeshift.Tests.Agents.Support;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Approval.Support;

// The agent tests' host, with two scripted rules and the calls the guarded tools made
public abstract class ApprovalTestBase : AgentTestBase
{
    protected ToolCalls ToolCalls { get; } = [];

    protected RuleScript Rules { get; } = new();

    // The guarded agent's host for the test's conversation
    protected AgentHost<GuardedAgent> Chat => Host<GuardedAgent>().WithConversationId(Data.Agent.ConversationId);

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
        Chat.RunAsync(input, settings: mode is null ? null : new() { Mode = mode }, cancellationToken: Ct);

    // The same run on either path, a streamed one as the response its updates make
    protected async Task<ChatResponse> RunAsync(string input, AgentMode? mode, bool streaming) =>
        streaming
            ? (await Chat.RunStreamingAsync(
                input,
                settings: mode is null ? null : new() { Mode = mode },
                cancellationToken: Ct)
                .ToListAsync(Ct))
            .ToChatResponse()
            : await RunAsync(input, mode);

    protected Task<ChatResponse> AnswerAsync(params DecisionAnswer[] answers) =>
        Chat.AnswerAsync(answers, cancellationToken: Ct);

    protected async Task<ChatResponse> StreamAsync(string input) =>
        (await Chat.RunStreamingAsync(input, cancellationToken: Ct).ToListAsync(Ct)).ToChatResponse();

    protected async Task<ChatResponse> StreamAnswerAsync(params DecisionAnswer[] answers) =>
        (await Chat.AnswerStreamingAsync(answers, cancellationToken: Ct).ToListAsync(Ct)).ToChatResponse();

    protected Task<ConversationDecisions> DecisionsAsync() => Chat.GetDecisionsAsync(Ct);

    // The conversation's decisions document as its store keeps it; null while nothing was ever written
    protected Task<StoredDocument?> StoredDecisionsAsync() =>
        Services.GetRequiredService<IDecisionStore<Guid>>().LoadDecisionsAsync(Data.Agent.ConversationId, Ct);

    protected ChatResponseUpdate CloseTicket(string callId, int number) =>
        Call(callId, "close_ticket", ("number", number));

    protected ChatResponseUpdate Note(string callId, string text) => Call(callId, "note", ("text", text));
}
