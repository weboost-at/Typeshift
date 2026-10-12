using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace Agentic.Generators.Emitting;

internal static class Names
{
    // The namespace of the modules and the interceptor; consumers opt into it through InterceptorsNamespaces
    public const string GeneratedNamespace = "Agentic.Generated";

    // search_web reads as SearchWeb in generated member names
    public static string ToPascalCase(string snakeCase) =>
        string.Concat(snakeCase.Split('_').Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));

    // The generated selection method of a tool; a name ToolSelection or object already uses gets a suffix
    public static string SelectorName(string tool) =>
        Unreserved(ToPascalCase(tool), "Tool", ["Equals", "GetHashCode", "GetType", "Select", "ToString"]);

    public static string OverridesName(string tool) => $"{ToPascalCase(tool)}Overrides";

    // The JSON naming of stored members: camelCase, as JsonNamingPolicy.CamelCase writes it for these names
    public static string JsonName(string member)
    {
        var upper = member.TakeWhile(char.IsUpper).Count();
        var lowered = upper > 1 && upper < member.Length ? upper - 1 : upper;

        return member.Substring(0, lowered).ToLowerInvariant() + member.Substring(lowered);
    }

    // A generated member named like a member it sits next to gets a suffix instead of clashing with it
    public static string Unreserved(string name, string suffix, ICollection<string> reserved) =>
        reserved.Contains(name) ? name + suffix : name;

    public static string Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;

    public static string Literal(string text) => SymbolDisplay.FormatLiteral(text, quote: true);

    public static string Module(string name) => $"global::{GeneratedNamespace}.{name}";

    public static string HintName(string fullName) => $"{fullName.Replace("global::", "")}.g.cs";
}
