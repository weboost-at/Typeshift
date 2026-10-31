using Microsoft.Extensions.AI;

namespace Typeshift;

// D46: what a run starts from: the stored messages, without the error messages and the calls that never got a result,
// since providers reject both, and without the requests that closed (D24, D60). A call that waited for approval
// is in the conversation twice: as the model made it, before its request, and as the tool loop made it again once the
// call was answered, next to its result; only the second stays, so each call goes out once
internal static class ConversationHistory
{
    public static List<ChatMessage> From(IReadOnlyList<ConversationEntry> entries)
    {
        var since = SinceSummary(entries);
        var messages = since.SelectMany(entry => entry.Messages).ToList();
        var contents = messages.SelectMany(message => message.Contents).ToList();
        var open = OpenRequests(since);
        var replied = contents.OfType<ToolApprovalResponseContent>().Select(answer => answer.RequestId).ToHashSet();

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

            if (Usable(
                    message,
                    call => answered.Contains(call) && (!requested.Contains(call) || asked.Contains(call)),
                    request => replied.Contains(request) || open.Contains(request))
                is { } kept)
                usable.Add(kept);
        }

        return usable;
    }

    // D24, D60: the requests the host may still answer, those the conversation's last run left without an answer and
    // answered nothing after, as it answers steering input. A later run dropped every request it had no answer for,
    // and a later answer of the same run went on without them, so an answer to one of those would run nothing
    public static HashSet<string> OpenRequests(IReadOnlyList<ConversationEntry> entries)
    {
        var since = SinceSummary(entries);

        // The last run begins after the last end another entry follows
        var start = since.Take(since.Count - 1).ToList().FindLastIndex(entry => entry.EndsRun) + 1;
        var replied = Replied(since);
        var open = new HashSet<string>();

        foreach (var entry in since.Skip(start))
        {
            if (!entry.EndsRun && !entry.IsSummary && Answers(entry.Messages)) open.Clear();
            open.UnionWith(RequestIds(entry.Messages).Where(request => !replied.Contains(request)));
        }

        return open;
    }

    // D60: why each request is not open: answered, passed by, or none the conversation holds since its summary
    public static Dictionary<string, ApprovalClosedReason> Closed(
        IReadOnlyList<ConversationEntry> entries,
        IEnumerable<string> requests)
    {
        var since = SinceSummary(entries);
        var replied = Replied(since);
        var asked = RequestIds(since.SelectMany(entry => entry.Messages)).ToHashSet();

        return requests.Distinct()
            .ToDictionary(
                request => request,
                request => replied.Contains(request) ? ApprovalClosedReason.Answered
                    : asked.Contains(request) ? ApprovalClosedReason.PassedBy
                    : ApprovalClosedReason.Unknown);
    }

    // Whether the agent answered in these messages: anything of its own but the approval requests it left
    public static bool Answers(IEnumerable<ChatMessage> messages) =>
        messages.Any(message => message.Role == ChatRole.Assistant
            && message.Contents.Any(content => content is not ToolApprovalRequestContent));

    public static IEnumerable<string> RequestIds(IEnumerable<ChatMessage> messages) =>
        messages.SelectMany(message => message.Contents)
            .OfType<ToolApprovalRequestContent>()
            .Select(request => request.RequestId);

    private static HashSet<string> Replied(IEnumerable<ConversationEntry> entries) =>
        entries.SelectMany(entry => entry.Messages)
            .SelectMany(message => message.Contents)
            .OfType<ToolApprovalResponseContent>()
            .Select(answer => answer.RequestId)
            .ToHashSet();

    // D32: compaction stored what it left of the history; what came before is in it
    public static IReadOnlyList<ConversationEntry> SinceSummary(IReadOnlyList<ConversationEntry> entries)
    {
        var start = entries.ToList().FindLastIndex(entry => entry.IsSummary);
        return [.. entries.Skip(Math.Max(start, 0))];
    }

    private static ChatMessage? Usable(
        ChatMessage message,
        Func<string, bool> keepsCall,
        Func<string, bool> keepsRequest)
    {
        var contents = message.Contents
            .Where(content => content is not ErrorContent)
            .Where(content => content is not FunctionCallContent call || keepsCall(call.CallId))
            .Where(content => content is not ToolApprovalRequestContent request || keepsRequest(request.RequestId))
            .ToList();

        if (contents.Count == message.Contents.Count) return message;
        if (contents.Count == 0) return null;

        var usable = message.Clone();
        usable.Contents = contents;
        return usable;
    }
}
