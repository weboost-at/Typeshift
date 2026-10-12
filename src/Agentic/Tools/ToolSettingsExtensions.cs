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
        /// Reads the settings a tool runs with; call it on the <see cref="IServiceProvider"/> the tool receives.
        /// </summary>
        /// <typeparam name="TSettings">The tool's settings record.</typeparam>
        /// <returns>The resolved settings of the running tool call, or the code defaults in any other scope.</returns>
        public TSettings GetToolSettings<TSettings>() where TSettings : class, new() =>
            services.GetService<ToolSettingsAccessor>()?.Settings as TSettings ?? new TSettings();
    }
}
