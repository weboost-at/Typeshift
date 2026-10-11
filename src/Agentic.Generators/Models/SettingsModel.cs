namespace Agentic.Generators.Models;

internal sealed record SettingsModel(string Type, EquatableArray<SettingsPropertyModel> Properties);

// Type is the declared type, NullableType the same type made nullable for the stored-value facades
internal sealed record SettingsPropertyModel(string Name, string Type, string NullableType, bool IsConfigurable);
