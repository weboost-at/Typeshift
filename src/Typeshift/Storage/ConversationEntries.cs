namespace Typeshift;

/// <summary>
/// Helpers for a conversation store's loads.
/// </summary>
public static class ConversationEntries
{
    extension(IReadOnlyList<ConversationEntry> entries)
    {
        /// <summary>
        /// Keeps the last summary and the entries after it, or every entry when compaction never summarized the
        /// conversation: what <see cref="IConversationStore{TId}.LoadConversationSinceCompactionAsync"/> returns. A
        /// store that cannot query from the last summary loads the whole conversation and returns this; one that can
        /// should start its query there instead, so a long conversation is not read whole on every run.
        /// </summary>
        /// <returns>The entries from the last summary on.</returns>
        public IReadOnlyList<ConversationEntry> SinceLastSummary() => ConversationHistory.SinceSummary(entries);
    }
}
