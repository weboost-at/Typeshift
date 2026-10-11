using System.ComponentModel;
using Agentic.Agents;
using Agentic.Catalogs;
using Agentic.Json;
using Agentic.Tools;
using Agentic.Toolsets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Agentic.Modules;

/// <summary>
/// The registrations generated code calls; not meant to be called by hand.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ModuleRegistration
{
    /// <summary>
    /// Registers the library's own services once.
    /// </summary>
    public static void AddCoreServices(IServiceCollection services)
    {
        services.AddOptions<AgentJsonOptions>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(provider => new AgentJson(provider.GetRequiredService<IOptions<AgentJsonOptions>>()));
        services.TryAddScoped(_ => new ToolSettingsAccessor());

        services.TryAddSingleton(provider => new ToolCatalog(
            provider.GetServices<Toolset>(),
            provider.GetRequiredService<AgentJson>()));

        services.TryAddSingleton(provider => new AgentCatalog(provider.GetServices<AgentDefinition>()));
    }

    /// <summary>
    /// Marks the module as registered, or returns false when it already is.
    /// </summary>
    public static bool TryAddModule(IServiceCollection services, Type module)
    {
        var registered = new RegisteredModule(module);
        if (services.Any(service => registered.Equals(service.ImplementationInstance))) return false;

        services.AddSingleton(registered);
        return true;
    }

    /// <summary>
    /// Registers a toolset as itself and as a <see cref="Toolset"/>.
    /// </summary>
    public static void AddToolset<TToolset>(IServiceCollection services, Func<TToolset> create)
        where TToolset : Toolset
    {
        services.AddSingleton(_ => create());
        services.AddSingleton<Toolset>(provider => provider.GetRequiredService<TToolset>());
    }

    /// <summary>
    /// Registers an agent as itself and as an <see cref="AgentDefinition"/>.
    /// </summary>
    public static void AddAgent<TAgent>(IServiceCollection services, Func<TAgent> create)
        where TAgent : AgentDefinition
    {
        services.AddSingleton(_ => create());
        services.AddSingleton<AgentDefinition>(provider => provider.GetRequiredService<TAgent>());
    }
}
