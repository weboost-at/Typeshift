using System.Linq;

namespace Typeshift.Generators.Models;

internal sealed record SettingsModel(string Type, EquatableArray<SettingsPropertyModel> Properties)
{
    public bool HasConfigurable => Properties.Any(property => property.IsConfigurable);
}

// Type keeps the nullable annotation, NullableType is it made nullable for the overrides
internal sealed record SettingsPropertyModel(
    string Name,
    string Type,
    string NullableType,
    bool IsConfigurable,
    string? Summary);
