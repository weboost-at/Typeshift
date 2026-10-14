using System.Linq;

namespace Typeshift.Generators.Models;

// Names are escaped identifiers; ContainingTypes hold declarations such as "class Outer", so the emitter can reopen
// them around the partial
internal sealed record TypeModel(
    string? Namespace,
    string Name,
    string FullName,
    string Accessibility,
    EquatableArray<string> ContainingTypes)
{
    // D6: unique in the namespace, for the top-level classes generated next to a nested type; Outer.Agent is
    // Outer_Agent, never OuterAgent, which a class of that name could already be
    public string FlatName =>
        string.Join("_", ContainingTypes.Select(container => container.Substring(container.LastIndexOf(' ') + 1)))
            .Replace("@", "")
        + (ContainingTypes.Count > 0 ? "_" : "")
        + Name.Replace("@", "");
}
