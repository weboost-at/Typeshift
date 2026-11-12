namespace Typeshift;

/// <summary>
/// Where <c>WithFileStore</c> keeps what it stores.
/// </summary>
public sealed record FileStoreOptions
{
    /// <summary>
    /// The folder of the file store, which keeps agents' own settings in its <c>settings</c> folder: by default
    /// <c>typeshift</c> under <see cref="AppContext.BaseDirectory"/>, beside your app's assemblies. A relative one
    /// resolves against the current directory when you register the store, which for a service may be the system's
    /// folder.
    /// </summary>
    public string Folder { get; set; } = Path.Combine(AppContext.BaseDirectory, "typeshift");
}
