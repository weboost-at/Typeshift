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

    public static string Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;

    public static string Literal(string text) => SymbolDisplay.FormatLiteral(text, quote: true);

    public static string Module(string name) => $"global::{GeneratedNamespace}.{name}";

    public static string HintName(string fullName) => $"{fullName.Replace("global::", "")}.g.cs";
}
