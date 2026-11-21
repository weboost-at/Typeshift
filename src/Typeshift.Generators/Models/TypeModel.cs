namespace Typeshift.Generators.Models;

// Names are escaped identifiers; ContainingTypes hold declarations such as "class Outer", so the emitter can reopen
// them around the partial
internal sealed record TypeModel(
    string? Namespace,
    string Name,
    string FullName,
    string Accessibility,
    EquatableArray<string> ContainingTypes);
