using Microsoft.Extensions.AI;

namespace Typeshift;

// D46: what a run starts from: the stored messages, as a provider accepts them. Each call's result follows the
// message that made the call, wherever a later entry stored it. Left out are the error messages, a call that never
// got a result, a result whose call is missing, and every copy of a call stored twice but its last. Requests for
// input and their answers, which a version before the decisions document stored (D80), are left out too: no
// provider receives one
internal static class ConversationHistory
{
    public static List<ChatMessage> From(IReadOnlyList<ConversationEntry> entries)
    {
        var messages = SinceSummary(entries).SelectMany(entry => entry.Messages).ToList();
        var pairs = new Pairs(messages);
        var usable = new List<ChatMessage>();

        foreach (var message in messages)
        {
            var kept = message.Contents
                .Where(content => content switch
                {
                    ErrorContent or InputRequestContent or InputResponseContent => false,
                    FunctionCallContent call => pairs.Keeps(call),
                    FunctionResultContent result => pairs.StaysIn(message, result),
                    _ => true
                })
                .ToList();

            if (With(message, kept) is { } left) usable.Add(left);

            usable.AddRange(pairs.ResultsOf(message));
        }

        return usable;
    }

    // D32: compaction stored what it left of the history; what came before is in it
    public static IReadOnlyList<ConversationEntry> SinceSummary(IReadOnlyList<ConversationEntry> entries)
    {
        var start = entries.ToList().FindLastIndex(entry => entry.IsSummary);
        return [.. entries.Skip(Math.Max(start, 0))];
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
                foreach (var content in message.Contents)
                {
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
