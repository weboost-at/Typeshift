using Microsoft.Extensions.AI;

namespace Typeshift;

internal static class ChatResponses
{
    // Two responses of one run, as one: the messages in order, the usage added up
    public static ChatResponse Merge(ChatResponse first, ChatResponse next)
    {
        var merged = new ChatResponse([.. first.Messages, .. next.Messages])
        {
            ModelId = next.ModelId ?? first.ModelId,
            FinishReason = next.FinishReason,
            Usage = first.Usage
        };

        if (next.Usage is { } usage) (merged.Usage ??= new UsageDetails()).Add(usage);
        return merged;
    }
}
