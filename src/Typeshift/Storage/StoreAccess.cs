using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Typeshift;

// D72: each id names the store of its own type, so the stores of an app may have several
internal sealed class StoreAccess(IServiceProvider services, IServiceCollection registrations)
{
    private readonly ConcurrentDictionary<Type, object> _memory = [];

    // D69: an app whose container holds no store, through WithStore or registered by the app itself, keeps everything
    // in memory, so a stock setup works; one with any store chose, and an interface no store of it implements throws
    private readonly Lazy<bool> _inMemory = new(() => !HoldsStores(registrations));

    // Every run asks, so the lookup runs once; a store whose creation failed is asked for again by the next run
    private readonly Lazy<IAgentSettingsStore?> _agentSettingsStore =
        new(services.GetService<IAgentSettingsStore>, LazyThreadSafetyMode.PublicationOnly);

    private readonly InMemoryTypeshiftStore<string> _agentSettingsMemory = new();

    private int _warned;

    // D66: whether runs have an agent's own settings to load, which no settings id reaches
    public bool KeepsAgentSettings => _inMemory.Value || _agentSettingsStore.Value is not null;

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

    public Task<StoredDocument?> LoadDecisionsAsync(StoreId conversation, CancellationToken ct) =>
        conversation.LoadDecisionsAsync(this, ct);

    public Task<long> SaveDecisionsAsync(StoreId conversation, string json, long loadedVersion, CancellationToken ct) =>
        conversation.SaveDecisionsAsync(this, json, loadedVersion, ct);

    // The store a write goes to; a load of nothing loses nothing, so the first write to the memory says where it goes
    public T Written<T>(T store) where T : class
    {
        if (_inMemory.Value
            && (ReferenceEquals(store, _agentSettingsMemory) || _memory.Values.Contains(store))
            && Interlocked.Exchange(ref _warned, 1) == 0)
        {
            services.GetService<ILoggerFactory>()?.CreateLogger("Typeshift").LogWarning(
                "No store is registered, so Typeshift keeps settings, conversations and decisions in memory and loses "
                + "them when the app stops: call WithStore<T>() after AddTypeshift() to keep them, or WithFileStore() "
                + "to keep agents' own settings in files.");
        }

        return store;
    }

    // D43: the stores are the persistence switch; the registrations are read before the container is asked, so a
    // store it may not hand out from the root is refused in Typeshift's words
    public ISettingsStore<TId> SettingsStore<TId>() where TId : notnull =>
        Memory<TId>()
        ?? services.GetService<ISettingsStore<TId>>()
        ?? throw new InvalidOperationException(
            $"No ISettingsStore<{TypeNames.Of<TId>()}> is registered for a settings id of that type: call "
            + "WithStore<T>() after AddTypeshift() with a store that implements it, or pass an id of your store's "
            + "type.");

    // D73: a conversation PersistAllConversations starts is under a Guid unless NewConversationIds gives other ids
    public IConversationStore<TId> ConversationStore<TId>() where TId : notnull =>
        Memory<TId>()
        ?? services.GetService<IConversationStore<TId>>()
        ?? throw new InvalidOperationException(
            $"No IConversationStore<{TypeNames.Of<TId>()}> is registered for a conversation id of that type: call "
            + "WithStore<T>() after AddTypeshift() with a store that implements it, or give ids of your store's "
            + "type, to WithConversationId and, for the conversations PersistAllConversations starts, with "
            + "NewConversationIds.");

    // D79: null when stores are registered and none keeps decisions for the id type. Nothing is kept in memory
    // beside a real store, so what a run asked would not vanish with the process while its conversation stays
    public IDecisionStore<TId>? DecisionStore<TId>() where TId : notnull =>
        Memory<TId>() ?? services.GetService<IDecisionStore<TId>>();

    public static InvalidOperationException NoDecisionStore<TId>() =>
        new(
            $"No IDecisionStore<{TypeNames.Of<TId>()}> is registered for a conversation id of that type, so nothing "
            + "can keep what a run asks your app or what your app approves for the conversation: call WithStore<T>() "
            + "after AddTypeshift() with a store that implements it beside your conversation store, such as an "
            + $"ITypeshiftStore<{TypeNames.Of<TId>()}>.");

    private IAgentSettingsStore AgentSettingsStore =>
        (_inMemory.Value ? _agentSettingsMemory : _agentSettingsStore.Value)
        ?? throw new InvalidOperationException(
            "No agent settings store is registered: call WithStore<T>() after AddTypeshift() with an "
            + "IAgentSettingsStore, such as InMemoryTypeshiftStore<TId>, to load or save an agent's own settings.");

    // One store per id type, made the first time an id of the type comes
    private InMemoryTypeshiftStore<TId>? Memory<TId>() where TId : notnull =>
        _inMemory.Value
            ? (InMemoryTypeshiftStore<TId>)_memory.GetOrAdd(typeof(TId), static _ => new InMemoryTypeshiftStore<TId>())
            : null;

    // D38, D72: a store interface of any id type, told by its definition, so no type is made at run time
    public static bool IsStoreInterface(Type type) =>
        type == typeof(IAgentSettingsStore)
        || (type.IsGenericType
            && type.GetGenericTypeDefinition() is var definition
            && (definition == typeof(ISettingsStore<>)
                || definition == typeof(IConversationStore<>)
                || definition == typeof(IDecisionStore<>)
                || definition == typeof(ITypeshiftStore<>)));

    private static Stored<TData>? Read<TData>(StoredDocument? document) where TData : class, IStorable<TData> =>
        document is null ? null : new Stored<TData>(TData.Deserialize(document.Json), document.Version);

    // A keyed store serves no run, which resolves stores without a key
    private static bool IsStoreRegistration(ServiceDescriptor registration) =>
        !registration.IsKeyedService && IsStoreInterface(registration.ServiceType);

    // D25: a store is one instance for every run, asked of the root. A scoped one the root refuses, or, without scope
    // validation, hands out once and for every request, so it fails here, where the message can name the way out
    private static bool HoldsStores(IServiceCollection registrations)
    {
        var stores = registrations.Where(IsStoreRegistration).ToList();

        if (stores.Find(store => store.Lifetime != ServiceLifetime.Singleton) is { } perScope)
        {
            var lifetime = perScope.Lifetime.ToString().ToLowerInvariant();

            throw new InvalidOperationException(
                $"{TypeNames.Of(perScope.ServiceType)} is registered as {lifetime}, but a store is one instance for "
                + "every run: register it with WithStore<T>() or AddSingleton, and let it take a factory, such as "
                + "IDbContextFactory<T>, for what it needs per call.");
        }

        return stores.Count > 0;
    }
}

// The C# names an error message gives the common id types, and the store interfaces of them
internal static class TypeNames
{
    public static string Of<T>() => Of(typeof(T));

    public static string Of(Type type) =>
        type switch
        {
            { IsGenericType: true } => $"{type.Name[..type.Name.IndexOf('`')]}<{Arguments(type)}>",
            { IsEnum: true } => type.Name,
            _ => Type.GetTypeCode(type) switch
            {
                TypeCode.Int32 => "int",
                TypeCode.Int64 => "long",
                TypeCode.Int16 => "short",
                TypeCode.String => "string",
                _ => type.Name
            }
        };

    private static string Arguments(Type type) => string.Join(", ", type.GenericTypeArguments.Select(Of));
}
