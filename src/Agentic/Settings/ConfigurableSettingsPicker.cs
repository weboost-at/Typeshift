using System.Linq.Expressions;

namespace Agentic.Settings;

/// <summary>
/// Picks the members that may change at runtime; the generator reads the picks, nothing evaluates them at runtime.
/// </summary>
public sealed class ConfigurableSettingsPicker<TSettings>
{
    internal ConfigurableSettingsPicker()
    {
    }

    /// <summary>
    /// Allows the picked properties to change at runtime; every other member stays as the code sets it.
    /// </summary>
    public ConfigurableSettingsPicker<TSettings> Configurable(
        params Expression<Func<TSettings, object?>>[] properties) =>
        this;
}
