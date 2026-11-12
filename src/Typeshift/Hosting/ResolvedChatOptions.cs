using Microsoft.Extensions.AI;

namespace Typeshift;

// The options ResolveOptionsAsync returns remember what else the run resolved, so the raw path's client gets it too:
// the providers (D77), the run options, the settings id (D26), and once a run begins, the run and its conversation
internal sealed class ResolvedChatOptions : ChatOptions
{
    public ResolvedChatOptions(ChatOptions options)
        : base(options)
    {
    }

    public ProviderId? Provider { get; set; }

    // D77: null for the run's provider
    public ProviderId? SummaryProvider { get; set; }

    public RunOptions RunOptions { get; set; } = new();

    // D52: a run whose settings name a mode leaves the conversation's mode behind
    public bool RunSetsMode { get; set; }

    public StoreId? SettingsId { get; set; }

    public StoreId? Conversation { get; set; }

    // D73: a run of the host's members, which the app continues with WithConversationId; the raw path names its
    // conversations in ChatOptions.ConversationId instead
    public bool HostRun { get; set; }

    public AgentRun? AgentRun { get; set; }

    public ConversationRun? ConversationRun { get; set; }

    // D33: the run that called this one as a sub-agent
    public AgentRun? Caller { get; set; }

    // D34, D50: the claim on the conversation a handoff or the agent passing a run on handed this run
    public RunClaim? Claim { get; set; }

    // D64: the format a typed run asked for, which an agent answering in its place answers in too
    public ChatResponseFormat? ResultFormat { get; set; }

    // A clone of the options, keeping what this type remembers
    public static ResolvedChatOptions From(ChatOptions options) =>
        options is ResolvedChatOptions resolved
            ? (ResolvedChatOptions)resolved.Clone()
            : new ResolvedChatOptions(options);

    public override ChatOptions Clone() =>
        new ResolvedChatOptions(this)
        {
            Provider = Provider,
            SummaryProvider = SummaryProvider,
            RunOptions = RunOptions,
            RunSetsMode = RunSetsMode,
            SettingsId = SettingsId,
            Conversation = Conversation,
            HostRun = HostRun,
            AgentRun = AgentRun,
            ConversationRun = ConversationRun,
            Caller = Caller,
            Claim = Claim,
            ResultFormat = ResultFormat
        };
}
