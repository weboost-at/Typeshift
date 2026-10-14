using Microsoft.Extensions.AI;

namespace Typeshift;

// The options ResolveOptionsAsync returns remember what else the run resolved, so the raw path's client gets it too:
// the run options, the settings id (D26), and once a run begins, its conversation
internal sealed class ResolvedChatOptions : ChatOptions
{
    public ResolvedChatOptions(ChatOptions options)
        : base(options)
    {
    }

    public RunOptions Run { get; set; } = new();

    public object? SettingsId { get; set; }

    public object? Conversation { get; set; }

    public ConversationRun? ConversationRun { get; set; }

    // A clone of the options, keeping what this type remembers
    public static ResolvedChatOptions From(ChatOptions options) =>
        options is ResolvedChatOptions resolved
            ? (ResolvedChatOptions)resolved.Clone()
            : new ResolvedChatOptions(options);

    public override ChatOptions Clone() =>
        new ResolvedChatOptions(this)
        {
            Run = Run,
            SettingsId = SettingsId,
            Conversation = Conversation,
            ConversationRun = ConversationRun
        };
}
