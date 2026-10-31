using Microsoft.Extensions.AI;

namespace Typeshift;

// The options ResolveOptionsAsync returns remember what else the run resolved, so the raw path's client gets it too:
// the providers (D56), the run options, the settings id (D26), and once a run begins, the run and its conversation
internal sealed class ResolvedChatOptions : ChatOptions
{
    public ResolvedChatOptions(ChatOptions options)
        : base(options)
    {
    }

    public object? Provider { get; set; }

    // D56: null for the run's provider
    public object? SummaryProvider { get; set; }

    public RunOptions RunOptions { get; set; } = new();

    // D52: a run whose settings name a mode leaves the conversation's mode behind
    public bool RunSetsMode { get; set; }

    public object? SettingsId { get; set; }

    public object? Conversation { get; set; }

    public AgentRun? AgentRun { get; set; }

    public ConversationRun? ConversationRun { get; set; }

    // D33: the run that called this one as a sub-agent
    public AgentRun? Caller { get; set; }

    // D34, D50: the claim on the conversation a handoff or the agent passing a run on handed this run
    public RunClaim? Claim { get; set; }

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
            AgentRun = AgentRun,
            ConversationRun = ConversationRun,
            Caller = Caller,
            Claim = Claim
        };
}
