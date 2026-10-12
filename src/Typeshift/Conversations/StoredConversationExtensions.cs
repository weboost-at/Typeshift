using Microsoft.Extensions.AI;

namespace Typeshift.Conversations;

/// <summary>
/// Reads a stored conversation across its sessions.
/// </summary>
public static class StoredConversationExtensions
{
    extension(StoredConversation conversation)
    {
        /// <summary>
        /// The usage of every session, summed.
        /// </summary>
        public UsageDetails AggregatedUsage => Sum(conversation.Sessions.Select(session => session.UsageDetails));

        /// <summary>
        /// The messages of every session, in session order.
        /// </summary>
        public IReadOnlyList<ChatMessage> AggregatedMessages =>
            [.. conversation.Sessions.SelectMany(session => session.Messages)];
    }

    private static UsageDetails Sum(IEnumerable<UsageDetails> usages)
    {
        var total = new UsageDetails();

        foreach (var usage in usages)
        {
            total.Add(usage);
        }

        return total;
    }
}
