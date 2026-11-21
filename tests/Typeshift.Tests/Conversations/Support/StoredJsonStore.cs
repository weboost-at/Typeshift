using System.Collections.Concurrent;

namespace Typeshift.Tests.Conversations.Support;

// Keeps a conversation as the JSON of its entries, so a test seeds what another version of a library stored
public sealed class StoredJsonStore : IConversationStore<Guid>
{
    private readonly ConcurrentDictionary<Guid, List<string>> _entries = [];

    public void Add(Guid conversation, string json)
    {
        var entries = _entries.GetOrAdd(conversation, _ => []);
        lock (entries) entries.Add(json);
    }

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct)
    {
        var entries = _entries.GetValueOrDefault(conversation) ?? [];

        lock (entries)
        {
            return Task.FromResult<IReadOnlyList<ConversationEntry>>(
                [.. entries.Select(ConversationEntry.Deserialize)]);
        }
    }

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct) =>
        (await LoadConversationAsync(conversation, ct)).SinceLastSummary();

    // As a unique key on the conversation and the number would, over the numbers the stored JSON holds
    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct)
    {
        var entries = _entries.GetOrAdd(conversation, _ => []);

        lock (entries)
        {
            var taken = entries.Any(json => ConversationEntry.Deserialize(json).Sequence == entry.Sequence);

            if (entry.Sequence > 0 && taken)
                throw new StoreConflictException($"Conversation {conversation} holds entry {entry.Sequence} already.");

            entries.Add(entry.Serialize());
        }

        return Task.CompletedTask;
    }
}
