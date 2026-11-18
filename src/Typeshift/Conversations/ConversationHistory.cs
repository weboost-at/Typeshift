using Microsoft.Extensions.AI;

namespace Typeshift;

// D46: what a run starts from: the stored messages, as a provider accepts them. Each call's result follows the
// message that made the call, wherever a later entry stored it. Left out are the error messages, a call that never
// got a result, a result whose call is missing, and every copy of a call stored twice but its last. Requests for
// input and their answers, which a version before the decisions document stored (D80), are left out too: no
// provider receives one
internal static class ConversationHistory
{
    // Built on every run, from every entry since the last summary: a message that keeps its contents, as nearly all
    // do, is taken as it is, and only one with a call looks for results
    public static List<ChatMessage> From(IReadOnlyList<ConversationEntry> entries)
    {
        var since = SinceSummary(entries);
        var messages = new List<ChatMessage>();

        for (var i = 0; i < since.Count; i++)
        {
            messages.AddRange(since[i].Messages);
        }

        var pairs = new Pairs(messages);
        var usable = new List<ChatMessage>(messages.Count);

        foreach (var message in messages)
        {
            if (KeepsAll(message, pairs)) usable.Add(message);
            else if (With(message, [.. message.Contents.Where(content => Keeps(content, message, pairs))]) is { } left)
                usable.Add(left);

            if (Calls(message)) usable.AddRange(pairs.ResultsOf(message));
        }

        return usable;
    }

    // D32: compaction stored what it left of the history; what came before is in it
    public static IReadOnlyList<ConversationEntry> SinceSummary(IReadOnlyList<ConversationEntry> entries)
    {
        var start = entries.Count - 1;
        while (start > 0 && !entries[start].IsSummary) start--;

        return start <= 0 ? entries : [.. entries.Skip(start)];
    }

    private static bool KeepsAll(ChatMessage message, Pairs pairs)
    {
        for (var i = 0; i < message.Contents.Count; i++)
        {
            if (!Keeps(message.Contents[i], message, pairs)) return false;
        }

        return true;
    }

    private static bool Keeps(AIContent content, ChatMessage message, Pairs pairs) =>
        content switch
        {
            ErrorContent or InputRequestContent or InputResponseContent => false,
            FunctionCallContent call => pairs.Keeps(call),
            FunctionResultContent result => pairs.StaysIn(message, result),
            _ => true
        };

    private static bool Calls(ChatMessage message)
    {
        for (var i = 0; i < message.Contents.Count; i++)
        {
            if (message.Contents[i] is FunctionCallContent) return true;
        }

        return false;
    }

    // D50: the highest number among the entries. An entry an earlier version stored has none and counts one past the
    // entry before it, so a conversation stored before entries were numbered goes on from the count of its entries
    public static long LastSequence(IReadOnlyList<ConversationEntry> entries)
    {
        long last = 0, highest = 0;

        foreach (var entry in entries)
        {
            last = entry.Sequence > 0 ? entry.Sequence : last + 1;
            highest = Math.Max(highest, last);
        }

        return highest;
    }

    // The message with these of its contents: itself when it keeps them all, none when it keeps nothing
    private static ChatMessage? With(ChatMessage message, List<AIContent> contents)
    {
        if (contents.Count == message.Contents.Count) return message;
        if (contents.Count == 0) return null;

        var kept = message.Clone();
        kept.Contents = contents;
        return kept;
    }

    // The calls that stay, and the result of each. Of a call or a result stored twice, by a store that retried an
    // append for example, the last copy counts
    private sealed class Pairs
    {
        private readonly Dictionary<string, FunctionCallContent> _calls = [];
        private readonly Dictionary<string, (FunctionResultContent Result, ChatMessage Message)> _results = [];

        public Pairs(List<ChatMessage> messages)
        {
            foreach (var message in messages)
            {
                for (var i = 0; i < message.Contents.Count; i++)
                {
                    var content = message.Contents[i];

                    if (content is FunctionCallContent call) _calls[call.CallId] = call;
                    if (content is FunctionResultContent result) _results[result.CallId] = (result, message);
                }
            }
        }

        public bool Keeps(FunctionCallContent call) =>
            ReferenceEquals(_calls[call.CallId], call) && _results.ContainsKey(call.CallId);

        // A result a provider returned in the message that made its call stays where it is
        public bool StaysIn(ChatMessage message, FunctionResultContent result) =>
            IsPaired(result) && message.Contents.Contains(_calls[result.CallId]);

        // The results of the message's calls that other messages hold, as the messages that stored them
        public IEnumerable<ChatMessage> ResultsOf(ChatMessage message)
        {
            var results = message.Contents
                .OfType<FunctionCallContent>()
                .Where(Keeps)
                .Select(call => _results[call.CallId])
                .Where(result => !ReferenceEquals(result.Message, message))
                .ToList();

            foreach (var stored in results.Select(result => result.Message).Distinct())
            {
                var paired = results.Where(result => ReferenceEquals(result.Message, stored))
                    .Select(result => (AIContent)result.Result)
                    .ToList();

                yield return With(stored, paired)!;
            }
        }

        private bool IsPaired(FunctionResultContent result) =>
            _results.TryGetValue(result.CallId, out var last)
            && ReferenceEquals(last.Result, result)
            && _calls.ContainsKey(result.CallId);
    }
}
