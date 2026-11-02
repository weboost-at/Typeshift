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

        return merged;
    }

    // The response without these approval requests, and without a message that held only them
    public static ChatResponse WithoutRequests(ChatResponse response, IReadOnlySet<string> requests)
    {
        if (requests.Count == 0) return response;

        var messages = response.Messages
            .Select(message =>
            {
                if (!message.Contents.Any(content => IsAmong(content, requests))) return message;

                var left = message.Clone();
                left.Contents = [.. message.Contents.Where(content => !IsAmong(content, requests))];
                return left.Contents.Count > 0 ? left : null;
            })
            .OfType<ChatMessage>()
            .ToList();

        return new ChatResponse(messages)
        {
            ModelId = response.ModelId,
            FinishReason = response.FinishReason,
            Usage = response.Usage,
            ResponseId = response.ResponseId,
            CreatedAt = response.CreatedAt,
            AdditionalProperties = response.AdditionalProperties
        };
    }

    private static bool IsAmong(AIContent content, IReadOnlySet<string> requests) =>
        content is ToolApprovalRequestContent request && requests.Contains(request.RequestId);
}
