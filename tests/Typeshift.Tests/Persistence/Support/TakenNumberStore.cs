using System.Collections.Concurrent;

namespace Typeshift.Tests.Persistence.Support;

// How a store gets the refusal of an entry under a number its conversation holds wrong
public enum TakenNumber
{
    // Stores it, as a table whose unique key a migration forgot
    Stored,

    // Refuses a number any conversation holds, as a unique key on the number alone
    KeyedAlone,

    // Refuses it with the database's own exception, which a run takes for a failed save
    DatabaseError,

    // Stores it and then refuses it, as a check after the insert
    StoredThenRefused
}

// Keeps conversations in memory and gets the refusal of an entry under a number its conversation holds wrong
public sealed class TakenNumberStore(TakenNumber fault) : IConversationStore<Guid>
{
    private readonly ConcurrentDictionary<Guid, List<ConversationEntry>> _rows = [];

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ConversationEntry>>([.. _rows.GetValueOrDefault(conversation) ?? []]);

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        (await LoadConversationAsync(conversation, cancellationToken)).SinceLastSummary();

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken cancellationToken)
    {
        var taken = Taken(conversation, entry.Sequence);

        if (taken && fault is TakenNumber.DatabaseError)
            throw new InvalidOperationException($"Duplicate key {entry.Sequence}.");

        if (taken && fault is TakenNumber.KeyedAlone)
            throw new StoreConflictException($"An entry numbered {entry.Sequence} is stored already.");

        _rows.GetOrAdd(conversation, _ => []).Add(ConversationEntry.Deserialize(entry.Serialize()));

        return taken && fault is TakenNumber.StoredThenRefused
            ? throw new StoreConflictException($"Conversation {conversation} holds entry {entry.Sequence} already.")
            : Task.CompletedTask;
    }

    // A number its conversation holds, or for a key on the number alone any conversation
    private bool Taken(Guid conversation, long sequence) =>
        _rows.Where(rows => fault is TakenNumber.KeyedAlone || rows.Key == conversation)
            .SelectMany(rows => rows.Value)
            .Any(entry => entry.Sequence == sequence);
}
