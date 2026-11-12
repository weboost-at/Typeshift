using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// Registers a store of the type, or the instance your app created, as each store interface it implements (D38)
internal delegate void StoreRegistration(
    IServiceCollection services,
    Type type,
    IReadOnlyList<Type> contracts,
    object? instance,
    Func<IServiceProvider, object> store);

/// <summary>
/// The registration chain <c>AddTypeshift()</c> starts.
/// </summary>
public sealed class TypeshiftBuilder
{
    private readonly IServiceCollection _services;
    private readonly StoreRegistration _addStore;
    private readonly Action<IServiceCollection, Func<IServiceProvider, object>> _addChatClientProvider;

    internal TypeshiftBuilder(
        IServiceCollection services,
        StoreRegistration addStore,
        Action<IServiceCollection, Func<IServiceProvider, object>> addChatClientProvider)
    {
        _services = services;
        _addStore = addStore;
        _addChatClientProvider = addChatClientProvider;
    }

    /// <summary>
    /// Registers a store, a singleton, and turns persistence on for what it keeps: agents' own settings, stored
    /// settings, conversations or several of them. Call it once per class, for example for a store of settings and
    /// another of conversations; for each store interface, the last call wins. Without it, every call that loads or
    /// saves throws, naming this method.
    /// </summary>
    /// <typeparam name="TStore">
    /// An <c>IAgentSettingsStore</c>, an <c>ISettingsStore&lt;TId&gt;</c>, an <c>IConversationStore&lt;TId&gt;</c>, or
    /// several of them, of any id types (TSH028). A run's ids pick the store of their type.
    /// </typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithStore<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)] TStore>()
        where TStore : class
    {
        _services.AddSingleton<TStore>();
        var store = typeof(TStore);
        _addStore(_services, store, Contracts(store), null, provider => provider.GetRequiredService<TStore>());
        return this;
    }

    /// <summary>
    /// Registers a store you created yourself, as each store interface <typeparamref name="TStore"/> implements, and as
    /// those of the build's id type the instance implements. Your app owns the instance, so the container never
    /// disposes it.
    /// </summary>
    /// <param name="store">The store, a singleton.</param>
    /// <typeparam name="TStore">
    /// The store's type as your code declares it: an <c>IAgentSettingsStore</c>, an <c>ISettingsStore&lt;TId&gt;</c>,
    /// an <c>IConversationStore&lt;TId&gt;</c>, or several of them, of any id types (TSH028).
    /// </typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithStore<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStore>(TStore store)
        where TStore : class
    {
        ArgumentNullException.ThrowIfNull(store);
        if (!typeof(TStore).IsInterface) _services.AddSingleton(store);
        _addStore(_services, typeof(TStore), Contracts(typeof(TStore)), store, _ => store);
        return this;
    }

    // A store the container builds, as WithStore<TStore>() does, from a factory: WithFileStore's over its folder
    internal TypeshiftBuilder WithStore<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStore>(
        Func<IServiceProvider, TStore> create)
        where TStore : class
    {
        _services.AddSingleton(create);
        var store = typeof(TStore);
        _addStore(_services, store, Contracts(store), null, provider => provider.GetRequiredService<TStore>());
        return this;
    }

    // D72: the store interfaces a type implements, of whatever id types, the type itself when it is one
    private static Type[] Contracts(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type store) =>
        [.. (store.IsInterface ? store.GetInterfaces().Append(store) : store.GetInterfaces()).Where(IsContract)];

    private static bool IsContract(Type contract) =>
        contract == typeof(IAgentSettingsStore)
        || (contract.IsGenericType
            && contract.GetGenericTypeDefinition() is var definition
            && (definition == typeof(ISettingsStore<>)
                || definition == typeof(IConversationStore<>)
                || definition == typeof(ITypeshiftStore<>)));

    /// <summary>
    /// Registers the provider that returns the client of a provider's model, scoped. It serves a run before any
    /// <see cref="Microsoft.Extensions.AI.IChatClient"/> registered under the provider or for every agent.
    /// </summary>
    /// <typeparam name="TProvider">An <c>IChatClientProvider</c> of the build's provider id type (TSH030).</typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithChatClientProvider<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>()
        where TProvider : class
    {
        _services.AddScoped<TProvider>();
        _addChatClientProvider(_services, provider => provider.GetRequiredService<TProvider>());
        return this;
    }

    /// <summary>
    /// Publishes the updates of streaming runs live, to <c>ConversationTracker.WatchAsync</c>.
    /// </summary>
    /// <param name="options">Intervals and limits; the defaults when null.</param>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithRealtimeTracking(RealtimeTrackingOptions? options = null)
    {
        _services.AddSingleton(options ?? new RealtimeTrackingOptions());
        return this;
    }

    /// <summary>
    /// Adds a rule that decides tool calls before the host is asked, scoped; any rule that rejects a call wins, then
    /// any that approves it.
    /// </summary>
    /// <typeparam name="TRule">The rule.</typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithApprovalRule<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRule>()
        where TRule : class, IToolApprovalRule
    {
        _services.AddScoped<IToolApprovalRule, TRule>();
        return this;
    }

    /// <summary>
    /// Adds middleware around every run of every agent, scoped; the first registered is the outermost.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware.</typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithRunMiddleware<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : class, IRunMiddleware
    {
        _services.AddScoped<IRunMiddleware, TMiddleware>();
        return this;
    }

    /// <summary>
    /// Adds middleware around every tool call of every agent, scoped; the first registered is the outermost.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware.</typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithToolMiddleware<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : class, IToolMiddleware
    {
        _services.AddScoped<IToolMiddleware, TMiddleware>();
        return this;
    }

    /// <summary>
    /// Registers what connects the MCP servers agents use, scoped.
    /// </summary>
    /// <typeparam name="TProvider">The provider.</typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithMcpToolProvider<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>()
        where TProvider : class, IMcpToolProvider
    {
        _services.AddScoped<IMcpToolProvider, TProvider>();
        return this;
    }
}
