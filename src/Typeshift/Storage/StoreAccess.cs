using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Typeshift;

// D72: each id names the store of its own type, so the stores of an app may have several
internal sealed class StoreAccess(IServiceProvider services)
{
    private readonly ConcurrentDictionary<Type, object> _memory = [];

    // D69: an app that registered no store with WithStore keeps in memory what no service of its container keeps, so
    // a stock setup works; one that registered any store chose, and an interface no store of it implements throws
    private readonly Lazy<bool> _inMemory = new(() => services.GetService<RegisteredStores>() is null);

    // Every run asks, so the lookup runs once
    private readonly Lazy<IAgentSettingsStore?> _agentSettingsStore = new(services.GetService<IAgentSettingsStore>);

    private readonly InMemoryTypeshiftStore<string> _agentSettingsMemory = new();

    private int _warned;

    // D66: whether runs have an agent's own settings to load, which no settings id reaches
    public bool KeepsAgentSettings => _agentSettingsStore.Value is not null || _inMemory.Value;

    public async Task<Stored<TData>?> LoadAsync<TData>(StoreId id, CancellationToken ct)
        where TData : class, IStorable<TData> =>
        Read<TData>(await id.LoadAsync(this, TData.Kind, ct));

    public Task<long> SaveAsync<TData>(StoreId id, TData data, long loadedVersion, CancellationToken ct)
        where TData : class, IStorable<TData> =>
        id.SaveAsync(this, TData.Kind, data.Serialize(), loadedVersion, ct);

    public async Task<Stored<TData>?> LoadAgentSettingsAsync<TData>(CancellationToken ct)
        where TData : class, IStorable<TData> =>
        Read<TData>(await AgentSettingsStore.LoadAsync(TData.Kind, ct));

    public Task<long> SaveAgentSettingsAsync<TData>(TData data, long loadedVersion, CancellationToken ct)
        where TData : class, IStorable<TData> =>
        Written(AgentSettingsStore).SaveAsync(TData.Kind, data.Serialize(), loadedVersion, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(StoreId conversation, CancellationToken ct) =>
        conversation.LoadConversationAsync(this, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        StoreId conversation,
        CancellationToken ct) =>
        conversation.LoadConversationSinceCompactionAsync(this, ct);

    public Task AppendConversationAsync(StoreId conversation, ConversationEntry entry, CancellationToken ct) =>
        conversation.AppendConversationAsync(this, entry, ct);

    // The store a write goes to; a load of nothing loses nothing, so the first write to the memory says where it goes
    public T Written<T>(T store) where T : class
    {
        if (_inMemory.Value
            && (ReferenceEquals(store, _agentSettingsMemory) || _memory.Values.Contains(store))
            && Interlocked.Exchange(ref _warned, 1) == 0)
        {
            services.GetService<ILoggerFactory>()?.CreateLogger("Typeshift").LogWarning(
                "No store is registered, so Typeshift keeps settings and conversations in memory and loses them when "
                + "the app stops: call WithStore<T>() after AddTypeshift() to keep them, or WithFileStore() to keep "
                + "agents' own settings in files.");
        }

        return store;
    }

    // D43: the stores are the persistence switch
    public ISettingsStore<TId> SettingsStore<TId>() where TId : notnull =>
        services.GetService<ISettingsStore<TId>>()
        ?? Memory<TId>()
        ?? throw new InvalidOperationException(
            $"No ISettingsStore<{TypeNames.Of<TId>()}> is registered for a settings id of that type: call "
            + "WithStore<T>() after AddTypeshift() with a store that implements it, or pass an id of your store's "
            + "type.");

    // D73: a conversation PersistAllConversations starts is under a Guid unless NewConversationIds gives other ids
    public IConversationStore<TId> ConversationStore<TId>() where TId : notnull =>
        services.GetService<IConversationStore<TId>>()
        ?? Memory<TId>()
        ?? throw new InvalidOperationException(
            $"No IConversationStore<{TypeNames.Of<TId>()}> is registered for a conversation id of that type: call "
            + "WithStore<T>() after AddTypeshift() with a store that implements it, or give ids of your store's "
            + "type, to WithConversationId and, for the conversations PersistAllConversations starts, with "
            + "NewConversationIds.");

    private IAgentSettingsStore AgentSettingsStore =>
        _agentSettingsStore.Value
        ?? (_inMemory.Value ? _agentSettingsMemory : null)
        ?? throw new InvalidOperationException(
            "No agent settings store is registered: call WithStore<T>() after AddTypeshift() with an "
            + "IAgentSettingsStore, such as InMemoryTypeshiftStore<TId>, to load or save an agent's own settings.");

    // One store per id type, made the first time an id of the type comes
    private InMemoryTypeshiftStore<TId>? Memory<TId>() where TId : notnull =>
        _inMemory.Value
            ? (InMemoryTypeshiftStore<TId>)_memory.GetOrAdd(typeof(TId), static _ => new InMemoryTypeshiftStore<TId>())
            : null;

    private static Stored<TData>? Read<TData>(StoredDocument? document) where TData : class, IStorable<TData> =>
        document is null ? null : new Stored<TData>(TData.Deserialize(document.Json), document.Version);
}

// Registered by WithStore, whatever the store's id type
internal sealed class RegisteredStores;

// The C# names an error message gives the common id types
internal static class TypeNames
{
    public static string Of<T>() =>
        typeof(T).IsEnum
            ? typeof(T).Name
            : Type.GetTypeCode(typeof(T)) switch
            {
                TypeCode.Int32 => "int",
                TypeCode.Int64 => "long",
                TypeCode.Int16 => "short",
                TypeCode.String => "string",
                _ => typeof(T).Name
            };
}
