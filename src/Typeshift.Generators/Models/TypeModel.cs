using System.Linq;

namespace Typeshift.Generators.Models;

// ContainingTypes hold declarations such as "class Outer", so the emitter can reopen them around the partial
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
