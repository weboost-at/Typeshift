using System.ComponentModel;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    /// <param name="services">The services.</param>
    /// <returns>The registration chain <c>AddTypeshift()</c> returns.</returns>
    public static TypeshiftBuilder AddCoreServices<TId>(IServiceCollection services) where TId : notnull
    {
        services.TryAddSingleton(provider => new ToolJson(
            provider.GetServices<JsonContextRegistration>().Select(registration => registration.Resolver)));

        services.TryAddSingleton<IStoreAccess>(provider => new StoreAccess<TId>(provider));

        return new TypeshiftBuilder(services, static (services, store) =>
            services.AddSingleton<ITypeshiftStore<TId>>(provider =>
                store(provider) as ITypeshiftStore<TId>
                ?? throw new InvalidOperationException(
                    $"The store is no ITypeshiftStore<{typeof(TId).Name}>, the build's id type (TSH028).")));
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

    private sealed record RegisteredModule(Type Module);

    private sealed record JsonContextRegistration(IJsonTypeInfoResolver Resolver);
}
