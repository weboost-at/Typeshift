namespace Typeshift;

// D72: an id with its type, which picks the store of that type, so ids of several types serve one app. Equal when
// type and value are, so a conversation of 5 as an int is another than 5 as a long
internal abstract record StoreId
{
    public abstract object Value { get; }

    public abstract Task<StoredDocument?> LoadAsync(StoreAccess stores, string kind, CancellationToken ct);

    public abstract Task<long> SaveAsync(
        StoreAccess stores,
        string kind,
        string json,
        long loadedVersion,
        CancellationToken ct);

    public abstract Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(
        StoreAccess stores,
        CancellationToken ct);

    public abstract Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        StoreAccess stores,
        CancellationToken ct);

    public abstract Task AppendConversationAsync(StoreAccess stores, ConversationEntry entry, CancellationToken ct);

    public sealed override string ToString() => Value.ToString() ?? "";
}

internal sealed record StoreId<TId>(TId Id) : StoreId where TId : notnull
{
    public override object Value => Id;

    public override Task<StoredDocument?> LoadAsync(StoreAccess stores, string kind, CancellationToken ct) =>
        stores.SettingsStore<TId>().LoadAsync(Id, kind, ct);

    public override Task<long> SaveAsync(
        StoreAccess stores,
        string kind,
        string json,
        long loadedVersion,
        CancellationToken ct) =>
        stores.SettingsStore<TId>().SaveAsync(Id, kind, json, loadedVersion, ct);

    public override Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(
        StoreAccess stores,
        CancellationToken ct) =>
        stores.ConversationStore<TId>().LoadConversationAsync(Id, ct);

    public override Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        StoreAccess stores,
        CancellationToken ct) =>
        stores.ConversationStore<TId>().LoadConversationSinceCompactionAsync(Id, ct);

    public override Task AppendConversationAsync(StoreAccess stores, ConversationEntry entry, CancellationToken ct) =>
        stores.ConversationStore<TId>().AppendConversationAsync(Id, entry, ct);
}
