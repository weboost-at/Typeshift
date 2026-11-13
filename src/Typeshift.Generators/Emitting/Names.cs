using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Typeshift.Generators.Models;
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

    // Generated plumbing that code completion leaves out, though only where the assembly is a compiled reference
    public const string Hidden =
        "[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]";

    // What every class has from object: a generated member of one of these names would hide it (D6)
    public static readonly string[] ObjectMembers =
        ["Equals", "GetHashCode", "GetType", "MemberwiseClone", "ReferenceEquals", "ToString"];

    // search_web reads as SearchWeb in generated member names
    public static string ToPascalCase(string snakeCase) =>
        string.Concat(snakeCase.Split('_').Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));

    // D6: a tool's property on its ToolList; a name the list already has from object gets a suffix
    public static string ToolProperty(string tool) => Unreserved(ToPascalCase(tool), "Tool", ObjectMembers);

    // D12: the name of a generated class in the global namespace: each part of the full name of the toolset or the
    // agent it belongs to, prefixed by its length, so no two classes share the global name
    public static string GlobalClassName(string prefix, TypeModel type)
    {
        var parts = (type.Namespace?.Split('.') ?? [])
            .Concat(type.ContainingTypes.Select(container => container.Substring(container.LastIndexOf(' ') + 1)))
            .Append(type.Name)
            .Select(part => part.Replace("@", ""));

        return prefix + string.Concat(parts.Select(part => $"{part.Length}{part}"));
    }

    public static string OverridesName(string tool) => $"{ToPascalCase(tool)}Overrides";

    public static string FactoryName(string tool) => $"Create{ToPascalCase(tool)}";

    public static string FunctionName(string tool) => $"{ToPascalCase(tool)}Function";

    // The members the generator adds for one tool to the toolset or the agent that declares it, which no member of the
    // user's may share
    public static IEnumerable<string> ToolsetMembers(string tool, bool hasOverrides) =>
        hasOverrides
            ? [OverridesName(tool), FactoryName(tool), FunctionName(tool)]
            : [FactoryName(tool), FunctionName(tool)];

    // The name two tools of one toolset or agent would both generate, null when they share none: an equal name in
    // PascalCase, as get_v2 and get_v_2 have, a property of the tool list, as equals and equals_tool are with the
    // suffix, or a member of the class, as create_report's function and report_function's factory are
    public static string? SharedMember(string tool, string other) =>
        ToPascalCase(tool) == ToPascalCase(other) ? ToPascalCase(tool)
        : ToolProperty(tool) == ToolProperty(other) ? ToolProperty(tool)
        : ClassMembers(tool).Intersect(ClassMembers(other)).FirstOrDefault();

    // With its overrides, which a tool has as soon as a setting of it becomes configurable
    private static IEnumerable<string> ClassMembers(string tool) => ToolsetMembers(tool, hasOverrides: true);

    public static string Unreserved(string name, string suffix, ICollection<string> reserved) =>
        reserved.Contains(name) ? name + suffix : name;

    public static string Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;

    public static string Literal(string text) => SymbolDisplay.FormatLiteral(text, quote: true);

    // A string array for an attribute's named argument
    public static string Array(IEnumerable<string> texts) =>
        $"new string[] {{ {string.Join(", ", texts.Select(Literal))} }}";

    // A hint name takes no '@', which a name spelled like a keyword carries
    public static string HintName(string fullName) => $"{fullName.Replace("global::", "").Replace("@", "")}.g.cs";

    // AddSource compares hint names without case, so a class whose name another one spells in another case carries a
    // hash of its own spelling; part names a second file of the class
    public static string HintName(string fullName, EquatableArray<string> caseClashes, string part = "") =>
        HintName(caseClashes.Contains(fullName) ? $"{fullName}-{GetStableHash(fullName)}{part}" : fullName + part);

    // The full names whose hint names differ from another's by case alone: none, in nearly every project
    public static EquatableArray<string> GetCaseClashes(IEnumerable<string> fullNames) =>
        fullNames
            .GroupBy(name => HintName(name), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Skip(1).Any())
            .SelectMany(group => group)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToEquatableArray();

    public static bool IsGenerated(SyntaxTree tree) => tree.FilePath.Contains(Generator);

    // FNV-1a: string.GetHashCode differs from run to run
    public static string GetStableHash(string text)
    {
        var hash = 2166136261;

        foreach (var character in text)
        {
            hash = unchecked((hash ^ character) * 16777619);
        }

        return hash.ToString("x8", CultureInfo.InvariantCulture);
    }
}
