using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Agentic.Settings;

internal sealed class TypedSettingsProperty<TSettings, TValue>(
    string name,
    bool isConfigurable,
    Func<TSettings, TValue> get,
    Func<TSettings, TValue, TSettings> with) : SettingsProperty<TSettings>(name, isConfigurable)
{
    internal override TSettings Apply(TSettings settings, JsonElement stored, JsonSerializerOptions options) =>
        with(settings, stored.Deserialize(GetTypeInfo(options))!);

    // A null override inherits, it never clears a value
    internal override TSettings Override(TSettings settings, TSettings overrides) =>
        get(overrides) is { } value ? with(settings, value) : settings;

    internal override JsonElement Export(TSettings settings, JsonSerializerOptions options) =>
        JsonSerializer.SerializeToElement(get(settings), GetTypeInfo(options));

    private static JsonTypeInfo<TValue> GetTypeInfo(JsonSerializerOptions options) =>
        (JsonTypeInfo<TValue>)options.GetTypeInfo(typeof(TValue));
}
