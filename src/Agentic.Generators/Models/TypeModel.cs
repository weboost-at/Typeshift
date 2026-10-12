using System.Linq;

namespace Agentic.Generators.Models;

// Where a partial class lives, so the emitter can reopen it; FullName is global::-qualified, ContainingTypes
// holds "class Outer" declarations and Accessibility the effective one
internal sealed record TypeModel(
    string? Namespace,
    string Name,
    string FullName,
    string Accessibility,
    EquatableArray<string> ContainingTypes)
{
    // Unique in the namespace, for the top-level classes generated next to a nested type
    public string FlatName =>
        string.Concat(ContainingTypes.Select(container => container.Substring(container.LastIndexOf(' ') + 1))) + Name;
}
