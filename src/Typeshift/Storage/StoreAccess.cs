using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D11: the library is compiled before the host picks its id type, so runs pass ids as objects to the store of the
// build's type, which the generated registration names
internal interface IStoreAccess
{
    Task<Stored<TData>?> LoadAsync<TData>(object id, CancellationToken ct) where TData : class, IStorable<TData>;

    Task<long> SaveAsync<TData>(object id, TData data, long expectedVersion, CancellationToken ct)
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
    // D43: the stores are the persistence switch
    private ISettingsStore<TId> SettingsStore =>
        services.GetService<ISettingsStore<TId>>()
        ?? throw new InvalidOperationException(
            "No settings store is registered: call WithStore<T>() after AddTypeshift() with an ISettingsStore to load "
            + "or save settings.");

    private IConversationStore<TId> ConversationStore =>
        services.GetService<IConversationStore<TId>>()
        ?? throw new InvalidOperationException(
            "No conversation store is registered: call WithStore<T>() after AddTypeshift() with an IConversationStore "
            + "to keep conversations.");

    public async Task<Stored<TData>?> LoadAsync<TData>(object id, CancellationToken ct)
        where TData : class, IStorable<TData> =>
        await SettingsStore.LoadAsync((TId)id, TData.Kind, ct) is { } document
            ? new Stored<TData>(TData.Deserialize(document.Json), document.Version)
            : null;

    public Task<long> SaveAsync<TData>(object id, TData data, long expectedVersion, CancellationToken ct)
        where TData : class, IStorable<TData> =>
        SettingsStore.SaveAsync((TId)id, TData.Kind, data.Serialize(), expectedVersion, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(object conversation, CancellationToken ct) =>
        ConversationStore.LoadConversationAsync((TId)conversation, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        object conversation,
        CancellationToken ct) =>
        ConversationStore.LoadConversationSinceCompactionAsync((TId)conversation, ct);

    public Task AppendConversationAsync(object conversation, ConversationEntry entry, CancellationToken ct) =>
        ConversationStore.AppendConversationAsync((TId)conversation, entry, ct);

    public object Parse(string id) =>
        parse is null
            ? throw new InvalidOperationException(
                $"{typeof(TId).Name} is not parsable, so ChatOptions.ConversationId cannot name a conversation: "
                + "run through the agent's generated members instead.")
            : parse(id);
}
