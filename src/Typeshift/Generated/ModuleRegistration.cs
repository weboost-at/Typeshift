using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Weboost.SchemaAnnotations;

namespace Typeshift.Generated;

/// <summary>
/// What a generated module registers: the core services once, then its toolsets, agents and JSON contexts.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ModuleRegistration
{
    /// <summary>
    /// Registers the services every module needs, once.
    /// </summary>
    /// <typeparam name="TId">The build's id type, <c>TypeshiftIdType</c>.</typeparam>
    /// <typeparam name="TProviderId">The build's provider id type, <c>TypeshiftProviderIdType</c>.</typeparam>
    /// <param name="services">The services.</param>
    /// <param name="parse">
    /// Reads an id from text, for the raw path's <c>ChatOptions.ConversationId</c>; null for an id type without one.
    /// </param>
    /// <returns>The registration chain <c>AddTypeshift()</c> returns.</returns>
    public static TypeshiftBuilder AddCoreServices<TId, TProviderId>(
        IServiceCollection services,
        Func<string, TId>? parse)
        where TId : notnull
        where TProviderId : notnull
    {
        services.TryAddSingleton(provider => new ToolJson(
            provider.GetServices<JsonContextRegistration>().Select(registration => registration.Resolver),
            provider.GetServices<SchemaAnnotationsRegistration>().Select(registration => registration.Source)));

        services.TryAddSingleton<IStoreAccess>(provider => new StoreAccess<TId>(provider, parse));
        services.TryAddSingleton<LiveRuns>();
        services.TryAddSingleton<InstrumentedClients>();
        services.TryAddSingleton<ChatClients, ChatClients<TProviderId>>();
        services.TryAddSingleton(provider => new ConversationTracker<TId>(
            provider.GetRequiredService<LiveRuns>(),
            provider.GetService<RealtimeTrackingOptions>()));

        return new TypeshiftBuilder(
            services,
            AddStore<TId>,
            static (services, clients) => services.AddScoped<IChatClientProvider<TProviderId>>(provider =>
                clients(provider) as IChatClientProvider<TProviderId>
                ?? throw new InvalidOperationException(
                    $"The chat client provider is no IChatClientProvider<{typeof(TProviderId).Name}>, of the build's "
                    + "provider id type (TSH030).")));
    }

    // D38: a store is registered as each store interface it implements; the type check reads no members. A store your
    // app created is checked as the instance it is, and registered as that instance, which the container never disposes
    private static void AddStore<TId>(
        IServiceCollection services,
        Type type,
        IReadOnlyList<Type> idTypes,
        object? instance,
        Func<IServiceProvider, object> store)
        where TId : notnull
    {
        bool Keeps<TStore>() => instance is null ? typeof(TStore).IsAssignableFrom(type) : instance is TStore;

        var keepsSettings = Keeps<ISettingsStore<TId>>();
        var keepsConversations = Keeps<IConversationStore<TId>>();
        var isTypeshiftStore = Keeps<ITypeshiftStore<TId>>();
        var keepsAgentSettings = Keeps<IAgentSettingsStore>();

        if ((!keepsSettings && !keepsConversations && !keepsAgentSettings) || idTypes.Any(id => id != typeof(TId)))
            throw new InvalidOperationException(
                $"{type.Name} is no store of the build's id type, {typeof(TId).Name}: implement "
                + $"ISettingsStore<{typeof(TId).Name}>, IConversationStore<{typeof(TId).Name}> or IAgentSettingsStore, "
                + "and no store interface of another id type (TSH028).");

        Add<ISettingsStore<TId>>(keepsSettings);
        Add<IConversationStore<TId>>(keepsConversations);
        Add<ITypeshiftStore<TId>>(isTypeshiftStore);
        Add<IAgentSettingsStore>(keepsAgentSettings);

        void Add<TStore>(bool keeps) where TStore : class
        {
            if (!keeps) return;

            if (instance is null) services.AddSingleton(provider => (TStore)store(provider));
            else services.AddSingleton((TStore)instance);
        }
    }

    /// <summary>
    /// Builds the app <c>TypeshiftApp.Create</c> returns, over the services its interceptor registered.
    /// </summary>
    /// <param name="services">The services, with the core services and the modules registered.</param>
    /// <param name="builder">The registration chain the core services returned.</param>
    /// <param name="client">The chat client of every run.</param>
    /// <param name="configure">What else the app registers; null for nothing.</param>
    /// <returns>The app.</returns>
    public static TypeshiftApp CreateApp(
        IServiceCollection services,
        TypeshiftBuilder builder,
        IChatClient client,
        Action<TypeshiftBuilder>? configure)
    {
        ArgumentNullException.ThrowIfNull(client);

        // D75: the client serves every agent, as one registered for every agent does (D63). An instance rather than
        // AddChatClient's factory, so the container never disposes it
        services.AddSingleton(client);
        configure?.Invoke(builder);

        return new TypeshiftApp(services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }));
    }

    /// <summary>
    /// Marks a module as registered.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="module">The module.</param>
    /// <returns>False when the module was registered before.</returns>
    public static bool TryAddModule(IServiceCollection services, Type module)
    {
        if (services.Any(service => service.ImplementationInstance is RegisteredModule { Module: var added }
                && added == module)) return false;

        services.AddSingleton(new RegisteredModule(module));
        return true;
    }

    /// <summary>
    /// Registers a toolset as a singleton.
    /// </summary>
    /// <typeparam name="TToolset">The toolset.</typeparam>
    /// <param name="services">The services.</param>
    /// <param name="create">Creates the toolset.</param>
    public static void AddToolset<TToolset>(IServiceCollection services, Func<TToolset> create)
        where TToolset : Toolset =>
        services.AddSingleton(_ => create());

    /// <summary>
    /// Registers an agent as a singleton and its host as transient.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="services">The services.</param>
    /// <param name="create">Creates the agent.</param>
    public static void AddAgent<TAgent>(IServiceCollection services, Func<TAgent> create)
        where TAgent : Agent
    {
        services.AddSingleton(_ => create());

        // D25: transient, so the host keeps the provider of the scope it is resolved from
        services.AddTransient(provider => new AgentHost<TAgent>(provider.GetRequiredService<TAgent>(), provider));
    }

    /// <summary>
    /// Registers a JSON context of the module's assembly, which tools ask before the defaults.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="context">The context.</param>
    public static void AddJsonContext(IServiceCollection services, IJsonTypeInfoResolver context) =>
        services.AddSingleton(new JsonContextRegistration(context));

    /// <summary>
    /// Registers the XML docs of the module's assembly, which describe what the tools' parameters hold.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="source">The docs Weboost.SchemaAnnotations collected.</param>
    public static void AddSchemaAnnotations(IServiceCollection services, ISchemaAnnotationSource source) =>
        services.AddSingleton(new SchemaAnnotationsRegistration(source));

    private sealed record RegisteredModule(Type Module);

    private sealed record JsonContextRegistration(IJsonTypeInfoResolver Resolver);

    private sealed record SchemaAnnotationsRegistration(ISchemaAnnotationSource Source);
}
