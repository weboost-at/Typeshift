using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Web;

// begin-snippet: chat-contracts
internal sealed record ChatInput(string Text);

// A decision writes its own JSON: its kind, its id, and what the UI shows to ask
internal sealed record ChatReply(string Text, IReadOnlyList<Decision> Decisions)
{
    public static ChatReply From(ChatResponse answer) => new(answer.Text, answer.Decisions);
}
// end-snippet

// begin-snippet: chat-line
// A line of the chat as its user reads it: the user's messages and the agents' answers, each once
internal sealed record ChatLine(string Role, string Text)
{
    public static IEnumerable<ChatLine> From(IReadOnlyList<ConversationEntry> entries) =>
        entries
            .Where(entry => !entry.IsSummary)
            .SelectMany(entry => entry.Messages)
            .Where(message => message.Role == ChatRole.User || message.Role == ChatRole.Assistant)
            .Where(message => message.Text.Length > 0)
            .Select(message => new ChatLine(message.Role.Value, message.Text));
}
// end-snippet
