using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Typeshift;

// D11: the library is compiled before the host picks its id type, so runs pass ids as objects to the store of the
// build's id type, which the generated registration names
internal interface IStoreAccess
{
    // D66: whether runs have an agent's own settings to load, which no settings id reaches
    bool KeepsAgentSettings { get; }

    Task<Stored<TData>?> LoadAsync<TData>(object id, CancellationToken ct) where TData : class, IStorable<TData>;

    Task<long> SaveAsync<TData>(object id, TData data, long loadedVersion, CancellationToken ct)
        where TData : class, IStorable<TData>;

    Task<Stored<TData>?> LoadAgentSettingsAsync<TData>(CancellationToken ct) where TData : class, IStorable<TData>;

    Task<long> SaveAgentSettingsAsync<TData>(TData data, long loadedVersion, CancellationToken ct)
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
    // D69: an app that registered no store at all keeps everything in memory, so a stock setup works; one that
    // registered any store chose, and an interface no store of it implements throws
    private readonly Lazy<InMemoryTypeshiftStore<TId>?> _memory = new(() =>
        services.GetService<ISettingsStore<TId>>() is null
        && services.GetService<IConversationStore<TId>>() is null
        && services.GetService<IAgentSettingsStore>() is null
            ? new InMemoryTypeshiftStore<TId>()
            : null);

    private int _warned;

    // D43: the stores are the persistence switch
    private ISettingsStore<TId> SettingsStore =>
        services.GetService<ISettingsStore<TId>>()
        ?? _memory.Value
        ?? throw new InvalidOperationException(
            "No settings store is registered: call WithStore<T>() after AddTypeshift() with an ISettingsStore to load "
            + "or save settings.");

    private IConversationStore<TId> ConversationStore =>
        services.GetService<IConversationStore<TId>>()
        ?? _memory.Value
        ?? throw new InvalidOperationException(
            "No conversation store is registered: call WithStore<T>() after AddTypeshift() with an IConversationStore "
            + "to keep conversations.");

    // Every run asks, so the lookup runs once
    private readonly Lazy<IAgentSettingsStore?> _agentSettingsStore = new(services.GetService<IAgentSettingsStore>);

    private IAgentSettingsStore AgentSettingsStore =>
        _agentSettingsStore.Value
        ?? _memory.Value
        ?? throw new InvalidOperationException(
            "No agent settings store is registered: call WithStore<T>() after AddTypeshift() with an "
            + "IAgentSettingsStore, such as InMemoryTypeshiftStore, to load or save an agent's own settings.");

    public bool KeepsAgentSettings => _agentSettingsStore.Value is not null || _memory.Value is not null;

    public async Task<Stored<TData>?> LoadAsync<TData>(object id, CancellationToken ct)
        where TData : class, IStorable<TData> =>
        Read<TData>(await SettingsStore.LoadAsync((TId)id, TData.Kind, ct));

    public Task<long> SaveAsync<TData>(object id, TData data, long loadedVersion, CancellationToken ct)
        where TData : class, IStorable<TData>
    {
        WarnOfMemory();
        return SettingsStore.SaveAsync((TId)id, TData.Kind, data.Serialize(), loadedVersion, ct);
    }

    public async Task<Stored<TData>?> LoadAgentSettingsAsync<TData>(CancellationToken ct)
        where TData : class, IStorable<TData> =>
        Read<TData>(await AgentSettingsStore.LoadAsync(TData.Kind, ct));

    public Task<long> SaveAgentSettingsAsync<TData>(TData data, long loadedVersion, CancellationToken ct)
        where TData : class, IStorable<TData>
    {
        WarnOfMemory();
        return AgentSettingsStore.SaveAsync(TData.Kind, data.Serialize(), loadedVersion, ct);
    }

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(object conversation, CancellationToken ct) =>
        ConversationStore.LoadConversationAsync((TId)conversation, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        object conversation,
        CancellationToken ct) =>
        ConversationStore.LoadConversationSinceCompactionAsync((TId)conversation, ct);

    public Task AppendConversationAsync(object conversation, ConversationEntry entry, CancellationToken ct)
    {
        WarnOfMemory();
        return ConversationStore.AppendConversationAsync((TId)conversation, entry, ct);
    }

    public object Parse(string id) =>
        parse is null
            ? throw new InvalidOperationException(
                $"{typeof(TId).Name} is not parsable, so ChatOptions.ConversationId cannot name a conversation: "
                + "run through the agent's generated members instead.")
            : parse(id);

    // A load of nothing loses nothing, so the first write says where the data goes
    private void WarnOfMemory()
    {
        if (_memory.Value is null || Interlocked.Exchange(ref _warned, 1) == 1) return;

        services.GetService<ILoggerFactory>()?.CreateLogger("Typeshift").LogWarning(
            "No store is registered, so Typeshift keeps settings and conversations in memory and loses them when the "
            + "app stops: call WithStore<T>() after AddTypeshift() to keep them.");
    }

    private static Stored<TData>? Read<TData>(StoredDocument? document) where TData : class, IStorable<TData> =>
        document is null ? null : new Stored<TData>(TData.Deserialize(document.Json), document.Version);
}
