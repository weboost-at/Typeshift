using System.ComponentModel;
using Agentic.Json;
using Agentic.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Agentic.DependencyInjection;

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
    }
}
