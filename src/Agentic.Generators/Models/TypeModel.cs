namespace Agentic.Generators.Models;

// Where a partial class lives, so the emitter can reopen it; FullName is global::-qualified
internal sealed record TypeModel(
    string? Namespace,
    string Name,
    string FullName,
    string Accessibility,
    EquatableArray<string> ContainingTypes);
