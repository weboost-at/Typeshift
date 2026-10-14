using System.Linq;

namespace Typeshift.Generators.Models;

internal sealed record SettingsModel(string Type, EquatableArray<SettingsPropertyModel> Properties)
{
    public bool HasConfigurable => Properties.Any(property => property.IsConfigurable);
}

// Type keeps the nullable annotation, NullableType is it made nullable for the overrides; a configurable member has
// the Storage its overrides member is written and read with
internal sealed record SettingsPropertyModel(
    string Name,
    string Type,
    string NullableType,
    StorageModel? Storage,
    string? Summary)
{
    public bool IsConfigurable => Storage is not null;
}
