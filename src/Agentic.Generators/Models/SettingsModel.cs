namespace Agentic.Generators.Models;

internal sealed record SettingsModel(string Type, EquatableArray<SettingsPropertyModel> Properties);

// NullableType is the member's type made nullable, for the overrides; Storage is set for configurable members
internal sealed record SettingsPropertyModel(
    string Name,
    string NullableType,
    bool IsConfigurable,
    StorageModel? Storage,
    string? Summary);
