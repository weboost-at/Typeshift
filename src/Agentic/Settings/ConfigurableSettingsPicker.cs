using System.Linq.Expressions;

namespace Agentic.Settings;

/// <summary>
/// Picks the members that may change at runtime; the generator reads the picks, nothing evaluates them at runtime.
/// </summary>
/// <typeparam name="TSettings">The settings record, or <c>ChatOptions</c>.</typeparam>
public sealed class ConfigurableSettingsPicker<TSettings>
{
    internal ConfigurableSettingsPicker()
    {
    }

    /// <summary>
    /// Allows the picked properties to change at runtime; every other member stays as the code sets it.
    /// </summary>
    /// <param name="properties">
    /// The properties, <c>s => s.Limit</c>; each needs a type settings can store (AGT014 says which).
    /// </param>
    /// <returns>The same picker, to pick further properties.</returns>
    public ConfigurableSettingsPicker<TSettings> Configurable(
        params Expression<Func<TSettings, object?>>[] properties) =>
        this;
}
