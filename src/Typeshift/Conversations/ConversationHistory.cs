using Microsoft.Extensions.AI;

namespace Typeshift;

// D46: what a run starts from: the stored messages, without the error messages and the calls that never got a result,
// since providers reject both. A call that waited for approval is in the conversation twice: as the model made it,
// before its request, and as the tool loop made it again once the call was answered, next to its result; only the
// second stays, so each call goes out once
internal static class ConversationHistory
{
    public static List<ChatMessage> From(IReadOnlyList<ConversationEntry> entries)
    {
        var messages = entries.SelectMany(entry => entry.Messages).ToList();
        var contents = messages.SelectMany(message => message.Contents).ToList();

        var answered = contents.OfType<FunctionResultContent>().Select(result => result.CallId).ToHashSet();

        var requested = contents
            .OfType<ToolApprovalRequestContent>()
            .Select(request => request.ToolCall)
            .OfType<FunctionCallContent>()
            .Select(call => call.CallId)
            .ToHashSet();

        var asked = new HashSet<string>();
        var usable = new List<ChatMessage>();

        foreach (var message in messages)
        {
            foreach (var request in message.Contents.OfType<ToolApprovalRequestContent>())
            {
                if (request.ToolCall is FunctionCallContent call) asked.Add(call.CallId);
            }

            if (Usable(message, call => answered.Contains(call) && (!requested.Contains(call) || asked.Contains(call)))
                is { } kept)
                usable.Add(kept);
        }

        return usable;
    }

    private static ChatMessage? Usable(ChatMessage message, Func<string, bool> keepsCall)
    {
        var contents = message.Contents
            .Where(content => content is not ErrorContent)
            .Where(content => content is not FunctionCallContent call || keepsCall(call.CallId))
            .ToList();

        if (contents.Count == message.Contents.Count) return message;
        if (contents.Count == 0) return null;

        var usable = message.Clone();
        usable.Contents = contents;
        return usable;
    }
}
