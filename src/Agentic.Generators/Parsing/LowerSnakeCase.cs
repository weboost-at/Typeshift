using System.Text.RegularExpressions;

namespace Agentic.Generators.Parsing;

internal static class LowerSnakeCase
{
    private static readonly Regex Pattern = new("^[a-z][a-z0-9]*(_[a-z0-9]+)*$");

    public static bool Matches(string name) => Pattern.IsMatch(name);
}
