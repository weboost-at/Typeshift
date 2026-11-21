using System.Collections.Concurrent;

namespace Typeshift.Tests.Persistence.Support;

// Orders a conversation by its entries' times, as an ORDER BY the timestamp would; entries of one time come back in
// an order of the database's choosing, here the newest first
public sealed class TimeOrderedStore : IConversationStore<Guid>
{
    private readonly ConcurrentDictionary<Guid, List<ConversationEntry>> _rows = [];

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct)
    {
        var rows = _rows.GetValueOrDefault(conversation) ?? [];

        return Task.FromResult<IReadOnlyList<ConversationEntry>>(
        [
            .. rows.Select((entry, index) => (entry, index))
                .OrderBy(row => row.entry.At)
                .ThenByDescending(row => row.index)
                .Select(row => row.entry)
        ]);
    }

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct) =>
        (await LoadConversationAsync(conversation, ct)).SinceLastSummary();

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct)
    {
        _rows.GetOrAdd(conversation, _ => []).Add(ConversationEntry.Deserialize(entry.Serialize()));
        return Task.CompletedTask;
    }
}
