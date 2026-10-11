using System.Text.Json;

namespace Agentic.Settings;

/// <summary>
/// Resolves one tool's settings record from its layers through the properties the generator lists.
/// </summary>
public sealed class ToolSettingsBinder<TSettings>(IReadOnlyList<SettingsProperty<TSettings>> properties)
    where TSettings : class, new()
{
    internal TSettings Resolve(ToolSettingsLayers layers, JsonSerializerOptions options)
    {
        var settings = layers.Agent as TSettings ?? Apply(new TSettings(), layers.Defaults, options);
        var configured = Apply(settings, layers.AgentOverrides, options);

        return layers.Run is TSettings run ? Override(configured, run) : configured;
    }

    // Only allowlisted members change, the agent's code settings above are the one layer that may set any member
    private TSettings Apply(TSettings settings, StoredSettings? stored, JsonSerializerOptions options)
    {
        if (stored is null) return settings;

        foreach (var property in Configurable)
        {
            if (stored.Values.TryGetValue(property.Name, out var value))
                settings = property.Apply(settings, value, options);
        }

        return settings;
    }

    private TSettings Override(TSettings settings, TSettings overrides) =>
        Configurable.Aggregate(settings, (current, property) => property.Override(current, overrides));

    private IEnumerable<SettingsProperty<TSettings>> Configurable =>
        properties.Where(property => property.IsConfigurable);
}
