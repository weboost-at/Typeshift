using Microsoft.Extensions.Logging;
using Typeshift;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the file store from options, on the chain <c>AddTypeshift()</c> starts.
/// </summary>
// In the namespace of AddTypeshift(), so the chain offers it to a file without a using of Typeshift
public static class FileStoreExtensions
{
    extension(TypeshiftBuilder builder)
    {
        /// <summary>
        /// Registers the file store, which keeps agents' own settings in a JSON file per agent, in the <c>settings</c>
        /// folder of the options' folder: a singleton the container builds, which waits on your app's
        /// <see cref="TimeProvider"/> when one is registered. For each store interface, the last call of this or
        /// <c>WithStore</c> wins.
        /// </summary>
        /// <param name="configure">Sets the options; the defaults when null.</param>
        /// <returns>The same chain.</returns>
        /// <exception cref="ArgumentException">The options' <c>Folder</c> is null or white space.</exception>
        public TypeshiftBuilder WithFileStore(Action<FileStoreOptions>? configure = null)
        {
            var options = new FileStoreOptions();
            configure?.Invoke(options);

            ArgumentException.ThrowIfNullOrWhiteSpace(options.Folder, "FileStoreOptions.Folder");

            // Resolved at registration, not at the first resolve; each kind of data gets a folder of its own
            var settings = Path.GetFullPath(Path.Combine(options.Folder, "settings"));

            return builder.WithStore(provider => new FileAgentSettingsStore(
                settings,
                provider.GetService<TimeProvider>(),
                provider.GetService<ILoggerFactory>()?.CreateLogger<FileAgentSettingsStore>()));
        }
    }
}
