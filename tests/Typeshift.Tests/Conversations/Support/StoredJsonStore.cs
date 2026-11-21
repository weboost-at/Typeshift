using System.Collections.Concurrent;

namespace Typeshift.Tests.Conversations.Support;

// Keeps a conversation as the JSON of its entries, so a test seeds what another version of a library stored
public sealed class StoredJsonStore : IConversationStore<Guid>
{
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<string>> _entries = [];

    public void Add(Guid conversation, string json) => _entries.GetOrAdd(conversation, _ => new()).Enqueue(json);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ConversationEntry>>(
            [.. (_entries.GetValueOrDefault(conversation) ?? []).Select(ConversationEntry.Deserialize)]);

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct) =>
        (await LoadConversationAsync(conversation, ct)).SinceLastSummary();

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct)
    {
        Add(conversation, entry.Serialize());
        return Task.CompletedTask;
    }
}
