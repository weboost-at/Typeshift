using Microsoft.Extensions.AI;

namespace Typeshift;

// An AgentHost<TAgent> where the agent's type is not known: a sub-agent, an agent handed off to (D33, D34)
internal interface IAgentHost
{
    Agent Agent { get; }

    // Runs with the stored settings of the id only; a sub-agent's run takes its caller's mode and approvals, and a run
    // handed a claim on the conversation takes it in place of claiming the conversation itself (D34, D50)
    Task<ChatResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        object? settingsId,
        object? conversation,
        AgentRun? caller,
        RunClaim? claim,
        CancellationToken ct);

    IAsyncEnumerable<ChatResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        object? settingsId,
        object? conversation,
        RunClaim? claim,
        CancellationToken ct);
}
