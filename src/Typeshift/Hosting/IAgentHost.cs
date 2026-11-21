using Microsoft.Extensions.AI;

namespace Typeshift;

// D83: the host of an agent your code does not name, which the runtime creates by kind or by type, and the library
// passes a sub-agent or an agent handed off to (D33, D34). Only AgentHost<TAgent> implements it
/// <summary>
/// The host of an agent whose type your code does not name: one <see cref="TypeshiftRuntime.CreateHost(string)"/>
/// creates by the agent's kind, or a scope resolves as a service keyed by it. It has the members of
/// <see cref="AgentHost{TAgent}"/> that take none of the agent's generated <c>Settings</c>, so one endpoint runs any
/// agent. Only the library implements it.
/// More in the guide: <see href="https://weboost-at.github.io/Typeshift/guide/registration#the-catalog">The
/// catalog</see>.
/// </summary>
public interface IAgentHost : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// The agent as its code declares it.
    /// </summary>
    Agent Agent { get; }

    /// <inheritdoc cref="AgentHost{TAgent}.Services"/>
    IServiceProvider Services { get; }

    /// <inheritdoc cref="AgentHost{TAgent}.WithSettingsId{TId}(TId)"/>
    IAgentHost WithSettingsId<TId>(TId id) where TId : notnull;

    /// <inheritdoc cref="AgentHost{TAgent}.WithSettingsId{TId}(Nullable{TId})"/>
    IAgentHost WithSettingsId<TId>(TId? id) where TId : struct;

    /// <inheritdoc cref="AgentHost{TAgent}.WithConversationId{TId}(TId)"/>
    IAgentHost WithConversationId<TId>(TId id) where TId : notnull;

    /// <inheritdoc cref="AgentHost{TAgent}.WithConversationId{TId}(Nullable{TId})"/>
    IAgentHost WithConversationId<TId>(TId? id) where TId : struct;

    /// <inheritdoc cref="AgentHost{TAgent}.RunAsync(string, CancellationToken)"/>
    Task<ChatResponse> RunAsync(string input, CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.RunAsync(ChatMessage, CancellationToken)"/>
    Task<ChatResponse> RunAsync(ChatMessage input, CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.RunAsync{TResult}(string, CancellationToken)"/>
    Task<ChatResponse<TResult>> RunAsync<TResult>(string input, CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.RunAsync{TResult}(ChatMessage, CancellationToken)"/>
    Task<ChatResponse<TResult>> RunAsync<TResult>(ChatMessage input, CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.RunStreamingAsync(string, CancellationToken)"/>
    StreamingRun RunStreamingAsync(string input, CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.RunStreamingAsync(ChatMessage, CancellationToken)"/>
    StreamingRun RunStreamingAsync(ChatMessage input, CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.RunStreamingAsync{TResult}(string, CancellationToken)"/>
    StreamingRun<TResult> RunStreamingAsync<TResult>(string input, CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.RunStreamingAsync{TResult}(ChatMessage, CancellationToken)"/>
    StreamingRun<TResult> RunStreamingAsync<TResult>(ChatMessage input, CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.AnswerAsync(IReadOnlyList{DecisionAnswer}, CancellationToken)"/>
    Task<ChatResponse> AnswerAsync(IReadOnlyList<DecisionAnswer> answers, CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.AnswerAsync{TResult}(IReadOnlyList{DecisionAnswer}, CancellationToken)"/>
    Task<ChatResponse<TResult>> AnswerAsync<TResult>(
        IReadOnlyList<DecisionAnswer> answers,
        CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.AnswerStreamingAsync(IReadOnlyList{DecisionAnswer}, CancellationToken)"/>
    StreamingRun AnswerStreamingAsync(IReadOnlyList<DecisionAnswer> answers, CancellationToken ct = default);

    /// <inheritdoc
    ///     cref="AgentHost{TAgent}.AnswerStreamingAsync{TResult}(IReadOnlyList{DecisionAnswer}, CancellationToken)"/>
    StreamingRun<TResult> AnswerStreamingAsync<TResult>(
        IReadOnlyList<DecisionAnswer> answers,
        CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.ResumeAsync(CancellationToken)"/>
    Task<ChatResponse> ResumeAsync(CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.ResumeStreamingAsync(CancellationToken)"/>
    StreamingRun ResumeStreamingAsync(CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.ResumeAsync{TResult}(CancellationToken)"/>
    Task<ChatResponse<TResult>> ResumeAsync<TResult>(CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.ResumeStreamingAsync{TResult}(CancellationToken)"/>
    StreamingRun<TResult> ResumeStreamingAsync<TResult>(CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.RetryAsync(CancellationToken)"/>
    Task<ChatResponse> RetryAsync(CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.RetryStreamingAsync(CancellationToken)"/>
    StreamingRun RetryStreamingAsync(CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.RetryAsync{TResult}(CancellationToken)"/>
    Task<ChatResponse<TResult>> RetryAsync<TResult>(CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.RetryStreamingAsync{TResult}(CancellationToken)"/>
    StreamingRun<TResult> RetryStreamingAsync<TResult>(CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.ResolveOptionsAsync(CancellationToken)"/>
    Task<ChatOptions> ResolveOptionsAsync(CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.CreateChatClientAsync(ChatOptions, CancellationToken)"/>
    Task<IChatClient> CreateChatClientAsync(ChatOptions options, CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.LoadConversationAsync(CancellationToken)"/>
    Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.GetDecisionsAsync(CancellationToken)"/>
    Task<ConversationDecisions> GetDecisionsAsync(CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.RecordAnswersAsync(IReadOnlyList{DecisionAnswer}, CancellationToken)"/>
    Task<ConversationDecisions> RecordAnswersAsync(
        IReadOnlyList<DecisionAnswer> answers,
        CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.CancelDecisionsAsync(CancellationToken)"/>
    Task<ConversationDecisions> CancelDecisionsAsync(CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.SetModeAsync(AgentMode, CancellationToken)"/>
    Task SetModeAsync(AgentMode mode, CancellationToken ct = default);

    /// <inheritdoc cref="AgentHost{TAgent}.RevokeStandingApprovalAsync(string, string, CancellationToken)"/>
    Task RevokeStandingApprovalAsync(string tool, string? server = null, CancellationToken ct = default);

    // Runs with the stored settings of the handover's settings id only, and with what else it was handed
    internal Task<ChatResponse> RunAsync(IEnumerable<ChatMessage> messages, RunHandover handover, CancellationToken ct);

    internal IAsyncEnumerable<ChatResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        RunHandover handover,
        CancellationToken ct);
}

// What one agent's run hands the run of another: the settings id its stored settings load under, the conversation it
// continues, and the format of a typed answer, which the other agent answers in too (D64). A run handed a claim on
// the conversation takes it in place of claiming the conversation itself (D34, D50), and counts on from the
// handoffs the run made before it. A run passed on to the agent that answers is handed the entries loaded for it
// too, so the conversation is loaded once. A sub-agent's run has its caller, whose mode and standing approvals it
// takes, and no conversation (D33). A retry continues a run that failed, too, and the agent that answers in this
// one's place retries (D50)
internal sealed record RunHandover(
    StoreId? SettingsId,
    StoreId? Conversation,
    RunClaim? Claim,
    ChatResponseFormat? Format,
    int Handoffs = 0,
    IReadOnlyList<ConversationEntry>? Entries = null,
    AgentRun? Caller = null,
    bool Retries = false);
