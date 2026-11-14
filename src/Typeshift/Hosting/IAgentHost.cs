using Microsoft.Extensions.AI;

namespace Typeshift;

// An AgentHost<TAgent> where the agent's type is not known: a sub-agent, an agent handed off to (D33, D34)
internal interface IAgentHost
{
    Agent Agent { get; }

    // Runs with the stored settings of the handover's settings id only, and with what else it was handed
    Task<ChatResponse> RunAsync(IEnumerable<ChatMessage> messages, RunHandover handover, CancellationToken ct);

    IAsyncEnumerable<ChatResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        RunHandover handover,
        CancellationToken ct);
}

// What one agent's run hands the run of another: the settings id its stored settings load under, the conversation it
// continues, and the format of a typed answer, which the other agent answers in too (D64). A run handed a claim on
// the conversation takes it in place of claiming the conversation itself (D34, D50). A sub-agent's run has its
// caller, whose mode and standing approvals it takes, and no conversation (D33)
internal sealed record RunHandover(
    StoreId? SettingsId,
    StoreId? Conversation,
    RunClaim? Claim,
    ChatResponseFormat? Format,
    AgentRun? Caller = null);
