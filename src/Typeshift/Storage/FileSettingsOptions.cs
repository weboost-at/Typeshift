namespace Typeshift;

/// <summary>
/// Where <c>WithFileSettings</c> keeps agents' own settings.
/// </summary>
public sealed record FileSettingsOptions
{
    /// <summary>
    /// The folder of the settings files, which the first save creates: by default <c>settings</c> under
    /// <see cref="AppContext.BaseDirectory"/>, beside your app's assemblies. A relative one resolves against the
    /// current directory when you register the store, which for a service may be the system's folder.
    /// </summary>
    public string Folder { get; set; } = Path.Combine(AppContext.BaseDirectory, "settings");
}
