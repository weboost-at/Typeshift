using System.Globalization;
using Typeshift.Persistence;

namespace Typeshift.Conversations;

// Parses the string conversation ids of ChatOptions into the id type of the registered store
internal abstract class ConversationStore
{
    public abstract bool Accepts(string conversationId);

    public abstract Task<StoredConversation?> LoadAsync(string conversationId, CancellationToken ct);

    public abstract Task SaveAsync(string conversationId, StoredConversation conversation, CancellationToken ct);
}

internal sealed class ConversationStore<TId>(ITypeshiftStore<TId> store) : ConversationStore
    where TId : notnull, IParsable<TId>
{
    public override bool Accepts(string conversationId) =>
        TId.TryParse(conversationId, CultureInfo.InvariantCulture, out _);

    public override Task<StoredConversation?> LoadAsync(string conversationId, CancellationToken ct) =>
        store.LoadAsync<StoredConversation>(Parse(conversationId), ct);

    public override Task SaveAsync(string conversationId, StoredConversation conversation, CancellationToken ct) =>
        store.SaveAsync(Parse(conversationId), conversation, ct);

    private static TId Parse(string conversationId) => TId.Parse(conversationId, CultureInfo.InvariantCulture);
}
