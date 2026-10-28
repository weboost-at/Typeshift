using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D11: the library is compiled before the host picks its id type, so runs pass ids as objects to the store of the
// build's type, which the generated registration names
internal interface IStoreAccess
{
    Task<Stored<TData>?> LoadAsync<TData>(object id, CancellationToken ct) where TData : class, IStorable<TData>;

    Task<long> SaveAsync<TData>(object id, TData data, long revision, CancellationToken ct)
        where TData : class, IStorable<TData>;

    Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(object conversation, CancellationToken ct);

    Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        object conversation,
        CancellationToken ct);

    Task AppendConversationAsync(object conversation, ConversationEntry entry, CancellationToken ct);

    // The raw path names its conversation in ChatOptions.ConversationId, a string
    object Parse(string id);
}

internal sealed class StoreAccess<TId>(IServiceProvider services, Func<string, TId>? parse) : IStoreAccess
    where TId : notnull
{
    // D43: the store is the persistence switch
    private ITypeshiftStore<TId> Store =>
        services.GetService<ITypeshiftStore<TId>>()
        ?? throw new InvalidOperationException(
            "No store is registered: call WithStore<T>() after AddTypeshift() to load or save settings.");

    public async Task<Stored<TData>?> LoadAsync<TData>(object id, CancellationToken ct)
        where TData : class, IStorable<TData> =>
        await Store.LoadAsync(TData.Kind, (TId)id, ct) is { } document
            ? new Stored<TData>(TData.Deserialize(document.Json, document.Version), document.Revision)
            : null;

    public Task<long> SaveAsync<TData>(object id, TData data, long revision, CancellationToken ct)
        where TData : class, IStorable<TData> =>
        Store.SaveAsync(TData.Kind, (TId)id, data.Serialize(), TData.Version, revision, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(object conversation, CancellationToken ct) =>
        Store.LoadConversationAsync((TId)conversation, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        object conversation,
        CancellationToken ct) =>
        Store.LoadConversationSinceCompactionAsync((TId)conversation, ct);

    public Task AppendConversationAsync(object conversation, ConversationEntry entry, CancellationToken ct) =>
        Store.AppendConversationAsync((TId)conversation, entry, ct);

    public object Parse(string id) =>
        parse is null
            ? throw new InvalidOperationException(
                $"{typeof(TId).Name} is not parsable, so ChatOptions.ConversationId cannot name a conversation: "
                + "run through the agent's generated members instead.")
            : parse(id);
}
