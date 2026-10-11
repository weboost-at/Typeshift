using System.Text.Json;

namespace Agentic.Settings;

/// <summary>
/// The base of the generated views over a tool's stored settings: one nullable property per allowlisted member.
/// </summary>
public abstract class StoredSettingsView
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Creates the view over the stored settings.
    /// </summary>
    protected StoredSettingsView(StoredSettings stored, JsonSerializerOptions options)
    {
        Stored = stored;
        _options = options;
    }

    /// <summary>
    /// The stored settings as changed through the view.
    /// </summary>
    public StoredSettings Stored { get; private set; }

    /// <summary>
    /// The stored value of a property, or null when it inherits.
    /// </summary>
    protected T? Get<T>(string property) => Stored.GetValue<T>(property, _options);

    /// <summary>
    /// Stores a value, or removes it when the value is null so the property inherits again.
    /// </summary>
    protected void Set<T>(string property, T? value) => Stored = Stored.WithValue(property, value, _options);
}
