using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Typeshift;

// D72: each id names the store of its own type, so the stores of an app may have several; the build's id type
// (D11) remains for the ids the generated members take, boxed, and for the raw path's ChatOptions.ConversationId
internal sealed class StoreAccess(IServiceProvider services, BuildIds build)
{
    private readonly ConcurrentDictionary<Type, object> _memory = [];

    // D69: an app that registered no store at all keeps everything in memory, so a stock setup works; one that
    // registered any store chose, and an interface no store of it implements throws
    private readonly Lazy<bool> _inMemory = new(() =>
        services.GetService<RegisteredStores>() is null
        && services.GetService<IAgentSettingsStore>() is null
        && !build.HasStore(services));

    // Every run asks, so the lookup runs once
    private readonly Lazy<IAgentSettingsStore?> _agentSettingsStore = new(services.GetService<IAgentSettingsStore>);

    private readonly InMemoryTypeshiftStore<string> _agentSettingsMemory = new();

    private int _warned;

    // D66: whether runs have an agent's own settings to load, which no settings id reaches
    public bool KeepsAgentSettings => _agentSettingsStore.Value is not null || _inMemory.Value;

    // The ids the generated members pass, of the build's id type
    public StoreId Wrap(object id) => build.Wrap(id);

    // The raw path names its conversation in ChatOptions.ConversationId, a string
    public StoreId Parse(string id) => build.Parse(id);

    public async Task<Stored<TData>?> LoadAsync<TData>(StoreId id, CancellationToken ct)
        where TData : class, IStorable<TData> =>
        Read<TData>(await id.LoadAsync(this, TData.Kind, ct));

    public Task<long> SaveAsync<TData>(StoreId id, TData data, long loadedVersion, CancellationToken ct)
        where TData : class, IStorable<TData>
    {
        WarnOfMemory();
        return id.SaveAsync(this, TData.Kind, data.Serialize(), loadedVersion, ct);
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

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(StoreId conversation, CancellationToken ct) =>
        conversation.LoadConversationAsync(this, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        StoreId conversation,
        CancellationToken ct) =>
        conversation.LoadConversationSinceCompactionAsync(this, ct);

    public Task AppendConversationAsync(StoreId conversation, ConversationEntry entry, CancellationToken ct)
    {
        WarnOfMemory();
        return conversation.AppendConversationAsync(this, entry, ct);
    }

    // D43: the stores are the persistence switch
    public ISettingsStore<TId> SettingsStore<TId>() where TId : notnull =>
        services.GetService<ISettingsStore<TId>>()
        ?? Memory<TId>()
        ?? throw new InvalidOperationException(
            $"No ISettingsStore<{TypeNames.Of<TId>()}> is registered for a settings id of that type: call "
            + "WithStore<T>() after AddTypeshift() with a store that implements it, or pass an id of your store's "
            + "type.");

    public IConversationStore<TId> ConversationStore<TId>() where TId : notnull =>
        services.GetService<IConversationStore<TId>>()
        ?? Memory<TId>()
        ?? throw new InvalidOperationException(
            $"No IConversationStore<{TypeNames.Of<TId>()}> is registered for a conversation id of that type: call "
            + "WithStore<T>() after AddTypeshift() with a store that implements it, or pass an id of your store's "
            + "type.");

    private IAgentSettingsStore AgentSettingsStore =>
        _agentSettingsStore.Value
        ?? (_inMemory.Value ? _agentSettingsMemory : null)
        ?? throw new InvalidOperationException(
            "No agent settings store is registered: call WithStore<T>() after AddTypeshift() with an "
            + "IAgentSettingsStore, such as InMemoryTypeshiftStore, to load or save an agent's own settings.");

    // One store per id type, made the first time an id of the type comes
    private InMemoryTypeshiftStore<TId>? Memory<TId>() where TId : notnull =>
        _inMemory.Value
            ? (InMemoryTypeshiftStore<TId>)_memory.GetOrAdd(typeof(TId), static _ => new InMemoryTypeshiftStore<TId>())
            : null;

    // A load of nothing loses nothing, so the first write says where the data goes
    private void WarnOfMemory()
    {
        if (!_inMemory.Value || Interlocked.Exchange(ref _warned, 1) == 1) return;

        services.GetService<ILoggerFactory>()?.CreateLogger("Typeshift").LogWarning(
            "No store is registered, so Typeshift keeps settings and conversations in memory and loses them when the "
            + "app stops: call WithStore<T>() after AddTypeshift() to keep them.");
    }

    private static Stored<TData>? Read<TData>(StoredDocument? document) where TData : class, IStorable<TData> =>
        document is null ? null : new Stored<TData>(TData.Deserialize(document.Json), document.Version);
}

// Registered by WithStore, whatever the store's id type
internal sealed class RegisteredStores;

// D11: the build's id type, which the library is compiled before
internal abstract class BuildIds
{
    public abstract StoreId Wrap(object id);

    public abstract StoreId Parse(string id);

    // A store of the build's id type registered without WithStore
    public abstract bool HasStore(IServiceProvider services);
}

internal sealed class BuildIds<TId>(Func<string, TId>? parse) : BuildIds where TId : notnull
{
    public override StoreId Wrap(object id) => new StoreId<TId>((TId)id);

    public override StoreId Parse(string id) =>
        parse is null
            ? throw new InvalidOperationException(
                $"{typeof(TId).Name} is not parsable, so ChatOptions.ConversationId cannot name a conversation: "
                + "run through the agent's generated members instead.")
            : new StoreId<TId>(parse(id));

    public override bool HasStore(IServiceProvider services) =>
        services.GetService<ISettingsStore<TId>>() is not null
        || services.GetService<IConversationStore<TId>>() is not null;
}

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
