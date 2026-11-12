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
    /// <param name="services">The services.</param>
    /// <returns>The registration chain <c>AddTypeshift()</c> returns.</returns>
    public static TypeshiftBuilder AddCoreServices(IServiceCollection services)
    {
        services.TryAddSingleton(provider => new ToolJson(
            provider.GetServices<JsonContextRegistration>().Select(registration => registration.Resolver),
            provider.GetServices<SchemaAnnotationsRegistration>().Select(registration => registration.Source)));

        // D69: the registrations too, so a store the app registers without WithStore turns the memory off
        services.TryAddSingleton(provider => new StoreAccess(provider, services));
        services.TryAddSingleton<LiveRuns>();
        services.TryAddSingleton<InstrumentedClients>();
        // D74: live updates are on from the start; WithRealtimeTracking tunes them
        services.TryAddSingleton(new RealtimeTrackingOptions());
        // D72: the tracker of every id type an app asks for; WithConversationTracker<TId>() closes one for Native AOT
        services.TryAddSingleton(typeof(ConversationTracker<>));

        // D76: the root's services, whatever scope resolves it
        services.TryAddSingleton(provider => new TypeshiftRuntime(provider));

        return new TypeshiftBuilder(services);
    }

    /// <summary>
    /// Builds the container <c>TypeshiftRuntime.Create</c> registered and returns its runtime, which owns it.
    /// </summary>
    /// <param name="services">The services, with the core services and the modules registered.</param>
    /// <param name="builder">The registration chain the core services returned.</param>
    /// <param name="client">The chat client of every run.</param>
    /// <param name="configure">What else the app registers; null for nothing.</param>
    /// <returns>The runtime.</returns>
    public static TypeshiftRuntime CreateRuntime(
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

        return TypeshiftRuntime.Owning(
            services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }));
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
        services.AddTransient(AgentHost<TAgent>.In);
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
