using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Typeshift;

/// <summary>
/// The registration chain <c>AddTypeshift()</c> starts.
/// </summary>
public sealed class TypeshiftBuilder
{
    private readonly IServiceCollection _services;

    internal TypeshiftBuilder(IServiceCollection services) => _services = services;

    /// <summary>
    /// The services the chain registers into: your app's own after <c>services.AddTypeshift()</c>, and those of the
    /// container <c>TypeshiftRuntime.Create</c> builds. Register here what the chain has no call for, such as logging
    /// or a service your tools resolve.
    /// </summary>
    public IServiceCollection Services => _services;

    /// <summary>
    /// Registers a store, a singleton, and turns persistence on for what it keeps: agents' own settings, stored
    /// settings, conversations, their decisions or several of them. Call it once per class, for example for a store of
    /// settings and another of conversations. A store interface has one store: a call whose store implements an
    /// interface another call's store is registered as throws. Without any store, Typeshift keeps everything in memory
    /// until the app stops.
    /// </summary>
    /// <param name="replace">
    /// True when the store takes over each interface it implements from the store registered as it before, as a test
    /// host's does.
    /// </param>
    /// <typeparam name="TStore">
    /// An <c>IAgentSettingsStore</c>, an <c>ISettingsStore&lt;TId&gt;</c>, an <c>IConversationStore&lt;TId&gt;</c>, an
    /// <c>IDecisionStore&lt;TId&gt;</c>, or several of them, of any id types (TSH028). A run's ids pick the store of
    /// their type.
    /// </typeparam>
    /// <returns>The same chain.</returns>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TStore"/> is no store, or another store is registered as one of its interfaces and
    /// <paramref name="replace"/> is false.
    /// </exception>
    public TypeshiftBuilder WithStore<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)] TStore>(
        bool replace = false)
        where TStore : class =>
        AddStore(
            typeof(TStore),
            new StoreClaim(typeof(TStore), null, TypeNames.Of<TStore>()),
            replace,
            ServiceDescriptor.Singleton<TStore, TStore>(),
            contract => ServiceDescriptor.Singleton(contract, Resolve<TStore>));

    /// <summary>
    /// Registers a store you created yourself, as each store interface <typeparamref name="TStore"/> implements. Your
    /// app owns the instance, so the container never disposes it.
    /// </summary>
    /// <param name="store">The store, a singleton.</param>
    /// <param name="replace">
    /// True when the store takes over each interface it implements from the store registered as it before.
    /// </param>
    /// <typeparam name="TStore">
    /// The store's type as your code declares it: an <c>IAgentSettingsStore</c>, an <c>ISettingsStore&lt;TId&gt;</c>,
    /// an <c>IConversationStore&lt;TId&gt;</c>, an <c>IDecisionStore&lt;TId&gt;</c>, or several of them, of any id
    /// types (TSH028).
    /// </typeparam>
    /// <returns>The same chain.</returns>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TStore"/> is no store, or another store is registered as one of its interfaces and
    /// <paramref name="replace"/> is false.
    /// </exception>
    public TypeshiftBuilder WithStore<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStore>(
        TStore store,
        bool replace = false)
        where TStore : class
    {
        ArgumentNullException.ThrowIfNull(store);

        return AddStore(
            typeof(TStore),
            new StoreClaim(typeof(TStore), store, TypeNames.Of(store.GetType())),
            replace,
            typeof(TStore).IsInterface ? null : new ServiceDescriptor(typeof(TStore), store),
            contract => new ServiceDescriptor(contract, store));
    }

    /// <summary>
    /// Registers a store the container builds with your factory, a singleton, as each store interface
    /// <typeparamref name="TStore"/> implements: for a store whose constructor takes what the container cannot give
    /// it, such as a folder or a connection string.
    /// </summary>
    /// <param name="create">Creates the store, once, from the root services.</param>
    /// <param name="replace">
    /// True when the store takes over each interface it implements from the store registered as it before.
    /// </param>
    /// <typeparam name="TStore">
    /// The store's type as the factory returns it: an <c>IAgentSettingsStore</c>, an
    /// <c>ISettingsStore&lt;TId&gt;</c>, an <c>IConversationStore&lt;TId&gt;</c>, an <c>IDecisionStore&lt;TId&gt;</c>,
    /// or several of them, of any id types (TSH028).
    /// </typeparam>
    /// <returns>The same chain.</returns>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TStore"/> is no store, or another store is registered as one of its interfaces and
    /// <paramref name="replace"/> is false.
    /// </exception>
    public TypeshiftBuilder WithStore<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStore>(
        Func<IServiceProvider, TStore> create,
        bool replace = false)
        where TStore : class
    {
        ArgumentNullException.ThrowIfNull(create);

        return AddStore(create, new StoreClaim(typeof(TStore), create, TypeNames.Of<TStore>()), replace);
    }

    // A store the container builds with a factory, under its declared type and behind each interface it implements.
    // One declared as an interface is that registration, and its other interfaces resolve it
    internal TypeshiftBuilder AddStore<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStore>(
        Func<IServiceProvider, TStore> create,
        StoreClaim store,
        bool replace)
        where TStore : class =>
        AddStore(
            typeof(TStore),
            store,
            replace,
            typeof(TStore).IsInterface ? null : ServiceDescriptor.Singleton(create),
            contract => contract == typeof(TStore)
                ? ServiceDescriptor.Singleton(contract, create)
                : ServiceDescriptor.Singleton(contract, Resolve<TStore>));

    // D38, D72: a store is registered as each store interface its declared type implements, of any id type; the type
    // check reads no members. A store your app created is registered as that instance, which the container never
    // disposes. A store interface another store holds is not taken over unless the call says so
    private TypeshiftBuilder AddStore(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type,
        StoreClaim store,
        bool replace,
        ServiceDescriptor? own,
        Func<Type, ServiceDescriptor> describe)
    {
        var contracts = Contracts(type);

        if (contracts.Length == 0)
            throw new InvalidOperationException(
                $"{type.Name} is no store: implement ISettingsStore<TId>, IConversationStore<TId>, "
                + "IDecisionStore<TId> or IAgentSettingsStore (TSH028).");

        StoreClaims.Of(_services).Add(_services, store, contracts, replace, own, describe);

        return this;
    }

    // The store the container built, behind each interface it is registered as
    private static object Resolve<TStore>(IServiceProvider provider) where TStore : class =>
        provider.GetRequiredService<TStore>();

    // D72: the store interfaces a type implements, of whatever id types, the type itself when it is one
    private static Type[] Contracts(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type store) =>
        [
            .. (store.IsInterface ? store.GetInterfaces().Append(store) : store.GetInterfaces())
                .Where(StoreAccess.IsStoreInterface)
        ];

    /// <summary>
    /// Registers the provider that returns the client of a provider's model, scoped, as each
    /// <see cref="IChatClientProvider{TProviderId}"/> it implements. It serves the runs of the agents whose provider
    /// is of its type, before any <see cref="Microsoft.Extensions.AI.IChatClient"/> registered under the provider or
    /// for every agent. For each provider type, the last call wins.
    /// </summary>
    /// <typeparam name="TProvider">
    /// An <c>IChatClientProvider&lt;TProviderId&gt;</c>, or several of them, of any provider types (TSH030).
    /// </typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithChatClientProvider<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)] TProvider>()
        where TProvider : class
    {
        // D77: as a store is (D72), of whatever provider types; the type check reads no members
        Type[] contracts = [.. typeof(TProvider).GetInterfaces().Where(IsChatClientProvider)];

        if (contracts.Length == 0)
            throw new InvalidOperationException(
                $"{typeof(TProvider).Name} is no chat client provider: implement IChatClientProvider<TProviderId> "
                + "(TSH030).");

        _services.AddScoped<TProvider>();

        foreach (var contract in contracts)
        {
            _services.AddScoped(contract, provider => provider.GetRequiredService<TProvider>());
        }

        return this;
    }

    private static bool IsChatClientProvider(Type contract) =>
        contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IChatClientProvider<>);

    /// <summary>
    /// Sets what every run of the app does, such as <c>options => options.PersistAllConversations = true</c>. Calls
    /// add up, each changing the options the last left.
    /// </summary>
    /// <param name="configure">Changes the options.</param>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithOptions(Action<TypeshiftOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = _services.Select(service => service.ImplementationInstance).OfType<TypeshiftOptions>()
            .FirstOrDefault();

        if (options is null) _services.AddSingleton(options = new TypeshiftOptions());

        configure(options);
        return this;
    }

    /// <summary>
    /// Registers the <see cref="ConversationTracker{TId}"/> of an id type as its own singleton, for an app published
    /// with Native AOT: there the container cannot make the tracker of a value type, such as <see cref="Guid"/> or
    /// <see cref="int"/>, from the one it has for every id type. Elsewhere, and for <see cref="string"/> ids, the
    /// container has it without this call. Inject or resolve the tracker itself: all trackers of the type, as an
    /// <c>IEnumerable</c>, are this one and the container's, and under Native AOT resolving them throws.
    /// </summary>
    /// <typeparam name="TId">The type of the conversation ids, as <c>WithConversationId</c> gives them.</typeparam>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithConversationTracker<TId>() where TId : notnull
    {
        // D72: closed here, so the container makes no generic type at run time
        _services.TryAddSingleton(provider => new ConversationTracker<TId>(
            provider.GetRequiredService<TypeshiftRuntime>()));

        return this;
    }

    /// <summary>
    /// Tunes the live updates of runs, which each <see cref="ConversationTracker{TId}"/> reads with <c>WatchAsync</c>;
    /// they are on without it.
    /// </summary>
    /// <param name="options">Intervals and limits; the defaults when null.</param>
    /// <returns>The same chain.</returns>
    public TypeshiftBuilder WithRealtimeTracking(RealtimeTrackingOptions? options = null)
    {
        _services.Replace(ServiceDescriptor.Singleton(options ?? new RealtimeTrackingOptions()));
        return this;
    }

    /// <summary>
    /// Adds a rule that decides tool calls before your app is asked, scoped; any rule that rejects a call wins, then
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
