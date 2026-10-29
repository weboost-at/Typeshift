using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Typeshift.Generators.Emitting;

internal static class Names
{
    // The namespace of the modules and the interceptor; consumers opt into it through InterceptorsNamespaces
    public const string GeneratedNamespace = "Typeshift.Generated";

    // A generated tree's path holds the generator's full name, which is how generated members are told from the
    // user's when the analyzer looks at both
    public const string Generator = "Typeshift.Generators.TypeshiftGenerator";

    // Generated plumbing the consumer's code completion leaves out
    public const string Hidden =
        "[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]";

    // search_web reads as SearchWeb in generated member names
    public static string ToPascalCase(string snakeCase) =>
        string.Concat(snakeCase.Split('_').Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));

    // D6: a tool's property on its ToolList; a name the list already has from object gets a suffix
    public static string ToolProperty(string tool) =>
        Unreserved(
            ToPascalCase(tool),
            "Tool",
            ["Equals", "GetHashCode", "GetType", "MemberwiseClone", "ReferenceEquals", "ToString"]);

    public static string OverridesName(string tool) => $"{ToPascalCase(tool)}Overrides";

    public static string FactoryName(string tool) => $"Create{ToPascalCase(tool)}";

    public static string FunctionName(string tool) => $"{ToPascalCase(tool)}Function";

    // The members the generator adds for one tool to the toolset or the agent that declares it, which no member of the
    // user's may share
    public static IEnumerable<string> ToolsetMembers(string tool, bool hasOverrides) =>
        hasOverrides
            ? [OverridesName(tool), FactoryName(tool), FunctionName(tool)]
            : [FactoryName(tool), FunctionName(tool)];

    public static string Unreserved(string name, string suffix, ICollection<string> reserved) =>
        reserved.Contains(name) ? name + suffix : name;

    public static string Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;

    public static string Literal(string text) => SymbolDisplay.FormatLiteral(text, quote: true);

    // A hint name takes no '@', which a name spelled like a keyword carries
    public static string HintName(string fullName) => $"{fullName.Replace("global::", "").Replace("@", "")}.g.cs";

    public static bool IsGenerated(SyntaxTree tree) => tree.FilePath.Contains(Generator);
}
