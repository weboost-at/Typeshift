using Typeshift;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers a <see cref="FileAgentSettingsStore"/> from options, on the chain <c>AddTypeshift()</c> starts.
/// </summary>
// In the namespace of AddTypeshift(), so the chain offers it to a file without a using of Typeshift
public static class FileSettingsExtensions
{
    extension(TypeshiftBuilder builder)
    {
        /// <summary>
        /// Registers a <see cref="FileAgentSettingsStore"/> over the options' folder as the store of agents' own
        /// settings, a singleton the container builds, which waits on your app's <see cref="TimeProvider"/> when one is
        /// registered. For each store interface, the last call of this or <c>WithStore</c> wins.
        /// </summary>
        /// <param name="configure">Sets the options; the defaults when null.</param>
        /// <returns>The same chain.</returns>
        /// <exception cref="ArgumentException">The options' <c>Folder</c> is null or white space.</exception>
        public TypeshiftBuilder WithFileSettings(Action<FileSettingsOptions>? configure = null)
        {
            var options = new FileSettingsOptions();
            configure?.Invoke(options);

            if (string.IsNullOrWhiteSpace(options.Folder))
                throw new ArgumentException(
                    "FileSettingsOptions.Folder is empty: set it to the folder that keeps the settings files.",
                    nameof(configure));

            // At registration, as WithStore(new FileAgentSettingsStore(folder)) resolves it, not at the first resolve
            var folder = Path.GetFullPath(options.Folder);

            return builder.WithStore(provider =>
                new FileAgentSettingsStore(folder, provider.GetService<TimeProvider>()));
        }
    }
}
