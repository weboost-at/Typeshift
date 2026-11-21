using Microsoft.Extensions.AI;

namespace Typeshift;

// The queries over messages and their contents that the run's layers share, so a content type Microsoft.Extensions.AI
// adds is handled in one place
internal static class ChatMessages
{
    extension(ChatMessage message)
    {
        // The message with the contents the predicate keeps: itself when it keeps them all, null when it keeps none,
        // and otherwise a clone, so the message a caller holds never changes
        public ChatMessage? Keeping(Func<AIContent, bool> keep)
        {
            var kept = 0;

            for (var i = 0; i < message.Contents.Count; i++)
            {
                if (keep(message.Contents[i])) kept++;
            }

            if (kept == message.Contents.Count) return message;
            return kept == 0 ? null : message.WithContents([.. message.Contents.Where(keep)]);
        }

        // The message with these of its own contents, as Keeping makes it
        public ChatMessage? WithContents(List<AIContent> contents)
        {
            if (contents.Count == message.Contents.Count) return message;
            if (contents.Count == 0) return null;

            var kept = message.Clone();
            kept.Contents = contents;
            return kept;
        }
    }

    extension(IEnumerable<ChatMessage> messages)
    {
        public IEnumerable<AIContent> Contents() => messages.SelectMany(message => message.Contents);
    }

    extension(IEnumerable<AIContent> contents)
    {
        // The calls the contents hold a result of
        public IEnumerable<string> ResultIds() =>
            contents.OfType<FunctionResultContent>().Select(result => result.CallId);
    }
}
