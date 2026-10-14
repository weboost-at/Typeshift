using System.ComponentModel;
using System.Text.Json.Serialization;
using Typeshift.Agents;
using Typeshift.Catalogs;
using Typeshift.Conversations;
using Typeshift.Json;
using Typeshift.Settings;
using Typeshift.Toolsets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Typeshift.Modules;

/// <summary>
/// The registrations generated code calls; not meant to be called by hand.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ModuleRegistration
{
    /// <summary>
    /// Registers the library's own services once.
    /// </summary>
    /// <param name="services">The host's services.</param>
    public static void AddCoreServices(IServiceCollection services)
    {
        services.AddOptions<AgentJsonOptions>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(provider => new AgentJson(provider.GetRequiredService<IOptions<AgentJsonOptions>>()));

        services.TryAddSingleton(provider => new ToolCatalog(
            provider.GetServices<Toolset>(),
            provider.GetRequiredService<AgentJson>()));

        services.TryAddSingleton(provider => new ToolDefaults(provider));

        services.TryAddSingleton(provider => new ConversationTracker(
            provider.GetService<ConversationStore>(),
            provider.GetRequiredService<TimeProvider>()));
    }

    /// <summary>
    /// Marks a module as registered.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="module">The generated module class.</param>
    /// <returns>True when the module was not registered yet, false when it already was.</returns>
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
    /// <typeparam name="TToolset">The toolset.</typeparam>
    /// <param name="services">The host's services.</param>
    /// <param name="create">Creates the toolset.</param>
    public static void AddToolset<TToolset>(IServiceCollection services, Func<TToolset> create)
        where TToolset : Toolset
    {
        services.AddSingleton(_ => create());
        services.AddSingleton<Toolset>(provider => provider.GetRequiredService<TToolset>());
    }

    /// <summary>
    /// Registers an agent, and its runtime as itself and as an <see cref="AgentRuntime"/>.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="services">The host's services.</param>
    /// <param name="create">Creates the agent.</param>
    public static void AddAgent<TAgent>(IServiceCollection services, Func<TAgent> create)
        where TAgent : AgentDefinition
    {
        services.AddSingleton(_ => create());
        services.AddSingleton(provider => new AgentRuntime<TAgent>(provider.GetRequiredService<TAgent>(), provider));

        services.AddSingleton<AgentRuntime>(provider => provider.GetRequiredService<AgentRuntime<TAgent>>());
    }

    /// <summary>
    /// Adds a JSON context that covers a module's tool types to the resolvers tool arguments and results serialize
    /// with, after the resolvers the host configures itself.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="context">The source-generated context.</param>
    public static void AddJsonContext(IServiceCollection services, JsonSerializerContext context) =>
        services.PostConfigure<AgentJsonOptions>(options =>
        {
            if (!options.TypeInfoResolverChain.Contains(context)) options.TypeInfoResolverChain.Add(context);
        });
}
