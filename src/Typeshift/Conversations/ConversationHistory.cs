using Microsoft.Extensions.AI;

namespace Typeshift;

// D46: what a run starts from: the stored messages, without the calls that never got a result and the error messages,
// since providers reject both
internal static class ConversationHistory
{
    public static List<ChatMessage> From(IReadOnlyList<ConversationEntry> entries)
    {
        var messages = entries.SelectMany(entry => entry.Messages).ToList();

        var answered = messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Select(result => result.CallId)
            .ToHashSet();

        return [.. messages.Select(message => Usable(message, answered)).OfType<ChatMessage>()];
    }

    private static ChatMessage? Usable(ChatMessage message, HashSet<string> answered)
    {
        var contents = message.Contents
            .Where(content => content is not ErrorContent)
            .Where(content => content is not FunctionCallContent call || answered.Contains(call.CallId))
            .ToList();

        if (contents.Count == message.Contents.Count) return message;
        if (contents.Count == 0) return null;

        return new ChatMessage(message.Role, contents)
        {
            AuthorName = message.AuthorName,
            CreatedAt = message.CreatedAt,
            MessageId = message.MessageId,
            AdditionalProperties = message.AdditionalProperties
        };
    }
}
