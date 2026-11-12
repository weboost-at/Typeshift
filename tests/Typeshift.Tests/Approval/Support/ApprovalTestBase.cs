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
        Host<GuardedAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(
            input,
            settings: mode is null ? null : new() { Mode = mode },
            ct: Ct);

    protected Task<ChatResponse> AnswerAsync(params AIContent[] answers) =>
        Host<GuardedAgent>().WithConversationId(Data.Agent.ConversationId).RunAsync(
            new ChatMessage(ChatRole.User, [.. answers]),
            ct: Ct);

    protected async Task<ChatResponse> StreamAsync(string input) =>
        (await Host<GuardedAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(input, ct: Ct)
            .ToListAsync(Ct))
        .ToChatResponse();

    protected async Task<ChatResponse> StreamAnswerAsync(params AIContent[] answers) =>
        (await Host<GuardedAgent>()
            .WithConversationId(Data.Agent.ConversationId)
            .RunStreamingAsync(
                new ChatMessage(ChatRole.User, [.. answers]),
                ct: Ct)
            .ToListAsync(Ct))
        .ToChatResponse();

    // The requests the conversation stored, whether a response returned them or not
    protected async Task<List<ToolApprovalRequestContent>> StoredRequestsAsync() =>
    [
        .. (await EntriesAsync(Data.Agent.ConversationId))
            .SelectMany(entry => entry.Messages)
            .SelectMany(message => message.Contents)
            .OfType<ToolApprovalRequestContent>()
    ];

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
