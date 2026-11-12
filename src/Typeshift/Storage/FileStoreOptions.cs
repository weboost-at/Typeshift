namespace Typeshift;

/// <summary>
/// Where <c>WithFileStore</c> keeps what it stores.
/// </summary>
public sealed record FileStoreOptions
{
    /// <summary>
    /// The root folder of the file store: by default <c>typeshift</c> under <see cref="AppContext.BaseDirectory"/>,
    /// beside your app's assemblies. A relative one resolves against the current directory when you register the
    /// store, which for a service may be the system's folder.
    /// </summary>
    public string Folder { get; set; } = Path.Combine(AppContext.BaseDirectory, "typeshift");

    /// <summary>
    /// Where agents' own settings are kept, a JSON file per agent.
    /// </summary>
    public SettingsOptions Settings { get; } = new();

    /// <summary>
    /// Where the file store keeps agents' own settings.
    /// </summary>
    public sealed record SettingsOptions
    {
        internal SettingsOptions()
        {
        }

        /// <summary>
        /// The folder of the settings files, which the first save creates: <c>settings</c> by default, relative to the
        /// store's <see cref="FileStoreOptions.Folder"/>. An absolute one is used as it is.
        /// </summary>
        public string Folder { get; set; } = "settings";
    }
}
