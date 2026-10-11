using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Tools;

/// <summary>
/// Reads the settings of the running tool call.
/// </summary>
public static class ToolSettingsExtensions
{
    extension(IServiceProvider services)
    {
        /// <summary>
        /// The resolved settings of the running tool call, or the code defaults outside a tool call.
        /// </summary>
        public TSettings GetToolSettings<TSettings>() where TSettings : class, new() =>
            services.GetService<ToolSettingsAccessor>()?.Settings as TSettings ?? new TSettings();
    }
}
