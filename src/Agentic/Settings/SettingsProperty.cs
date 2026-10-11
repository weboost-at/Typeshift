using System.Text.Json;

namespace Agentic.Settings;

/// <summary>
/// One member of a settings record, read and copied without reflection.
/// </summary>
public abstract class SettingsProperty<TSettings>
{
    private protected SettingsProperty(string name, bool isConfigurable)
    {
        Name = name;
        IsConfigurable = isConfigurable;
    }

    /// <summary>
    /// The property name, also the key of its stored value.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Whether the property may change at runtime.
    /// </summary>
    public bool IsConfigurable { get; }

    internal abstract TSettings Apply(TSettings settings, JsonElement stored, JsonSerializerOptions options);

    internal abstract TSettings Override(TSettings settings, TSettings overrides);
}

/// <summary>
/// Creates the settings properties the generated code lists.
/// </summary>
public static class SettingsProperty
{
    /// <summary>
    /// A property read by <paramref name="get"/> and set on a copy by <paramref name="with"/>.
    /// </summary>
    public static SettingsProperty<TSettings> Create<TSettings, TValue>(
        string name,
        bool isConfigurable,
        Func<TSettings, TValue> get,
        Func<TSettings, TValue, TSettings> with) =>
        new TypedSettingsProperty<TSettings, TValue>(name, isConfigurable, get, with);
}
