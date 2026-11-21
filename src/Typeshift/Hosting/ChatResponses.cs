using Microsoft.Extensions.AI;

namespace Typeshift;

internal static class ChatResponses
{
    // Two responses of one run, as one: the messages in order, the usage added up in a sum of its own
    public static ChatResponse Merge(ChatResponse first, ChatResponse next)
    {
        var merged = new ChatResponse([.. first.Messages, .. next.Messages])
        {
            ModelId = next.ModelId ?? first.ModelId,
            FinishReason = next.FinishReason
        };

        foreach (var usage in new[] { first.Usage, next.Usage }.OfType<UsageDetails>())
        {
            (merged.Usage ??= new UsageDetails()).Add(usage);
        }

        if (first.ConversationIncomplete || next.ConversationIncomplete) Incomplete(merged);
        return merged;
    }

    // D46: a run that was not stored whole says so on what it returns
    public static void Incomplete(ChatResponse response) =>
        (response.AdditionalProperties ??= [])[TypeshiftExtensions.Incomplete] = true;

    public static ChatResponseUpdate IncompleteUpdate() =>
        new() { AdditionalProperties = new() { [TypeshiftExtensions.Incomplete] = true } };
}
