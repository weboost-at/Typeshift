using System.Reflection;
using System.Text.RegularExpressions;

namespace Typeshift.Generators.Tests.Diagnostics.Support;

// A catalog's sources by the names of its fields, so a theory's rows read as the fields
public static partial class SourceCatalogs
{
    public static IEnumerable<string> NamesOf(Type catalog) =>
        catalog.GetFields(BindingFlags.Public | BindingFlags.Static).Select(field => field.Name);

    public static string Read(Type catalog, string name) => (string)catalog.GetField(name)!.GetValue(null)!;

    // The sources that hold a failing example of docs/guide/diagnostics.md: mdsnippets copies the region named
    // after a diagnostic, tsh005 for TSH005, into that diagnostic's section
    public static IEnumerable<(string Id, string Name)> ExamplesOf(Type catalog) =>
        from name in NamesOf(catalog)
        from Match example in Example().Matches(Read(catalog, name))
        select (example.Groups["id"].Value.ToUpperInvariant(), name);

    [GeneratedRegex(@"// begin-snippet: (?<id>tsh\d{3})\b")]
    private static partial Regex Example();
}
