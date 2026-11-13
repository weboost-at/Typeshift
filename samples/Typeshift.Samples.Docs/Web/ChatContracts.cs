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
