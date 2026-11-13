using System.Reflection;

namespace Typeshift.Generators.Tests.Diagnostics.Support;

// A catalog's sources by the names of its fields, so a theory's rows read as the fields
public static class SourceCatalogs
{
    public static IEnumerable<string> NamesOf(Type catalog) =>
        catalog.GetFields(BindingFlags.Public | BindingFlags.Static).Select(field => field.Name);

    public static string Read(Type catalog, string name) => (string)catalog.GetField(name)!.GetValue(null)!;
}
