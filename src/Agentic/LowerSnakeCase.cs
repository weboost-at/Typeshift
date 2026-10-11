using System.Text.RegularExpressions;

namespace Agentic;

internal static partial class LowerSnakeCase
{
    public static bool Matches(string name) => Pattern().IsMatch(name);

    [GeneratedRegex("^[a-z][a-z0-9]*(_[a-z0-9]+)*$")]
    private static partial Regex Pattern();
}
