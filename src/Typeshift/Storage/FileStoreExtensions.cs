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
        /// <see cref="TimeProvider"/> when one is registered. Another store that keeps agents' own settings beside it
        /// throws, the one in memory apart, which leaves them to the files.
        /// </summary>
        /// <param name="configure">Sets the options; the defaults when null.</param>
        /// <param name="replace">
        /// True when the file store takes agents' own settings over from the store registered for them before.
        /// </param>
        /// <returns>The same chain.</returns>
        /// <exception cref="ArgumentException">The options' <c>Folder</c> is null or white space.</exception>
        /// <exception cref="InvalidOperationException">
        /// Another store keeps agents' own settings and <paramref name="replace"/> is false.
        /// </exception>
        public TypeshiftBuilder WithFileStore(Action<FileStoreOptions>? configure = null, bool replace = false)
        {
            var options = new FileStoreOptions();
            configure?.Invoke(options);

            ArgumentException.ThrowIfNullOrWhiteSpace(options.Folder, "FileStoreOptions.Folder");

            // Resolved at registration, not at the first resolve; each kind of data gets a folder of its own
            var folder = Path.GetFullPath(options.Folder);
            var settings = Path.Combine(folder, "settings");

            // D38: the folder tells one file store from another, so the same call twice is one store
            return builder.AddStore(
                provider => new FileAgentSettingsStore(
                    settings,
                    provider.GetService<TimeProvider>(),
                    LogEvents.For(provider)),
                new StoreClaim(typeof(FileAgentSettingsStore), folder, $"WithFileStore(Folder = '{folder}')"),
                replace);
        }
    }
}
