using System.Collections.Concurrent;

namespace Typeshift.Tests.Persistence.Support;

// Orders a conversation by its entries' times, as an ORDER BY the timestamp would; entries of one time come back in
// an order of the database's choosing, here the newest first
public sealed class TimeOrderedStore : IConversationStore<Guid>
{
    private readonly ConcurrentDictionary<Guid, List<ConversationEntry>> _rows = [];

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(
        Guid conversation,
        CancellationToken cancellationToken)
    {
        var rows = _rows.GetValueOrDefault(conversation) ?? [];

        lock (rows)
        {
            return Task.FromResult<IReadOnlyList<ConversationEntry>>(
            [
                .. rows.Select((entry, index) => (entry, index))
                    .OrderBy(row => row.entry.At)
                    .ThenByDescending(row => row.index)
                    .Select(row => row.entry)
            ]);
        }
    }

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        (await LoadConversationAsync(conversation, cancellationToken)).SinceLastSummary();

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken cancellationToken)
    {
        var rows = _rows.GetOrAdd(conversation, _ => []);

        lock (rows)
        {
            if (entry.Sequence > 0 && rows.Any(row => row.Sequence == entry.Sequence))
                throw new StoreConflictException($"Conversation {conversation} holds entry {entry.Sequence} already.");

            rows.Add(ConversationEntry.Deserialize(entry.Serialize()));
        }

        return Task.CompletedTask;
    }
}
