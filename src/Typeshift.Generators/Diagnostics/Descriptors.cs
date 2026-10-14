using Microsoft.CodeAnalysis;

namespace Typeshift.Generators.Diagnostics;

// D49: numbered afresh for the rewrite; after the first release an id never changes
internal static class Descriptors
{
    private const string Category = "Typeshift";

    private const string HelpUrl = "https://weboost-at.github.io/Typeshift/guide/diagnostics#";

    public static readonly DiagnosticDescriptor NotPartial = Error(
        "TSH001",
        "Toolset or agent is not partial",
        "'{0}' must be partial: the generator adds members to it");

    public static readonly DiagnosticDescriptor NotConstant = Error(
        "TSH002",
        "Name or description is not a constant",
        "The {0} must be a constant: the generator reads it at compile time");

    public static readonly DiagnosticDescriptor NotStaticMethodGroup = Error(
        "TSH003",
        "Tool is not a static method group",
        "'{0}' is not a static method group: pass a static method, AddTool(Method)");

    public static readonly DiagnosticDescriptor UnboundParameter = Error(
        "TSH004",
        "Tool parameter cannot be bound",
        "The parameter '{0}' of the tool '{1}' cannot be bound from the model's arguments");

    public static readonly DiagnosticDescriptor SettingsNotRecord = Error(
        "TSH005",
        "Settings type is not a record with a parameterless constructor",
        "The settings type '{0}' must be a record: each layer applies to a copy of the code defaults");

    public static readonly DiagnosticDescriptor SettingsLessAccessible = Error(
        "TSH006",
        "Settings type is less accessible than its toolset",
        "The settings type '{0}' must be as accessible as the toolset '{1}'");

    public static readonly DiagnosticDescriptor DuplicateToolName = Error(
        "TSH007",
        "Tool name is not unique",
        "Another tool is already named '{0}', or maps to the same member '{1}'");

    public static readonly DiagnosticDescriptor NotLowerSnakeCase = Error(
        "TSH008",
        "Tool name is not lower_snake_case",
        "The tool name '{0}' is not lower_snake_case; name the tool with WithName(\"...\")");

    public static readonly DiagnosticDescriptor MissingDescription = Warning(
        "TSH009",
        "Tool has no description",
        "The tool '{0}' has no description: add a <summary> to its method or WithDescription(...)");

    public static readonly DiagnosticDescriptor UnreadSettings = Warning(
        "TSH010",
        "Tool does not take its settings",
        "The tool '{0}' takes no parameter of its settings type '{1}', so its settings reach nothing");

    public static readonly DiagnosticDescriptor DocumentationOff = Warning(
        "TSH011",
        "Tool relies on XML docs that are not generated",
        "The tool '{0}' takes its descriptions from XML docs, which the compiler reads only with "
        + "GenerateDocumentationFile");

    public static readonly DiagnosticDescriptor NotSettable = Error(
        "TSH013",
        "Configurable member cannot be set",
        "The configurable member '{0}' has no public set or init accessor");

    public static readonly DiagnosticDescriptor OutsideConstructor = Error(
        "TSH014",
        "Declaration outside the constructor",
        "'{0}' is read by the generator only directly in the constructor");

    public static readonly DiagnosticDescriptor UnreadableChain = Error(
        "TSH015",
        "Chain the generator cannot read",
        "The generator cannot read this {0}: keep the declaration in one chain and pick members as s => s.Member");

    public static readonly DiagnosticDescriptor Unreachable = Error(
        "TSH016",
        "Toolset or agent is out of the generated code's reach",
        "'{0}' is out of the generated code's reach: {1}");

    public static readonly DiagnosticDescriptor NotDirectlyDerived = Error(
        "TSH017",
        "Toolset or agent is generic or derives from another",
        "'{0}' {1}: derive it from {2} directly, without type parameters");

    public static readonly DiagnosticDescriptor GeneratedNameClash = Error(
        "TSH018",
        "Member clashes with a generated member",
        "The member '{0}' clashes with a member the generator writes for '{1}'");

    private static DiagnosticDescriptor Error(string id, string title, string message) =>
        Create(id, title, message, DiagnosticSeverity.Error);

    private static DiagnosticDescriptor Warning(string id, string title, string message) =>
        Create(id, title, message, DiagnosticSeverity.Warning);

    private static DiagnosticDescriptor Create(string id, string title, string message, DiagnosticSeverity severity) =>
        new(
            id,
            title,
            message,
            Category,
            severity,
            isEnabledByDefault: true,
            helpLinkUri: HelpUrl + id.ToLowerInvariant());
}
