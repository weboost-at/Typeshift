using Microsoft.Extensions.AI;

namespace Typeshift;

// The options ResolveOptionsAsync returns remember what else the run resolved, so the raw path's client gets it too
internal sealed class ResolvedChatOptions : ChatOptions
{
    public ResolvedChatOptions(ChatOptions options)
        : base(options)
    {
    }

    public RunOptions Run { get; set; } = new();

    public override ChatOptions Clone() => new ResolvedChatOptions(this) { Run = Run };
}
