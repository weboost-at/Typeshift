using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Agentic.Generators.Diagnostics;

internal static class Descriptors
{
    private const string Category = "Agentic";

    public static readonly DiagnosticDescriptor NotPartial = Error(
        "AGT001",
        "Toolset or agent is not partial",
        "'{0}' must be declared partial so the generator can add its members");

    public static readonly DiagnosticDescriptor NotConstant = Error(
        "AGT002",
        "Tool name or description is not a constant",
        "The tool {0} must be a compile-time constant string");

    public static readonly DiagnosticDescriptor MissingToolName = Error(
        "AGT003",
        "Tool has no name",
        "AddTool({0}) has no WithName(...)");

    public static readonly DiagnosticDescriptor NotStaticMethodGroup = Error(
        "AGT004",
        "Tool is not a static method group",
        "AddTool needs a static method group, '{0}' is not one");

    public static readonly DiagnosticDescriptor SettingsNotRecord = Error(
        "AGT005",
        "Settings type is not a record",
        "The settings type '{0}' must be a record, so stored values apply to a 'with' copy");

    public static readonly DiagnosticDescriptor DuplicateToolName = Error(
        "AGT008",
        "Tool name is not unique",
        "The tool name '{0}' is declared by {1}");

    public static readonly DiagnosticDescriptor MissingDescription = Warning(
        "AGT009",
        "Tool has no description",
        "The tool '{0}' has no description: add WithDescription(...) or an XML <summary> to its method");

    public static readonly DiagnosticDescriptor SettingsAsParameter = Error(
        "AGT011",
        "Tool takes its settings as a parameter",
        "The parameter '{0}' of the tool '{1}' has the settings type '{2}': read settings with GetToolSettings<T>()");

    public static readonly DiagnosticDescriptor DocumentationOff = Warning(
        "AGT013",
        "Tool relies on XML docs that are not generated",
        "The tool '{0}' relies on XML documentation, but GenerateDocumentationFile is off");

    public static readonly DiagnosticDescriptor UngeneratedReference = Warning(
        "AGT014",
        "Referenced assembly was not compiled with the generator",
        "'{0}' declares toolsets or agents but was not compiled with the generator, so AddAgentic() cannot "
        + "register them: reference the Agentic package in its project");

    public static readonly DiagnosticDescriptor NotLowerSnakeCase = Error(
        "AGT015",
        "Tool name is not lower_snake_case",
        "The tool name '{0}' is not lower_snake_case");

    private static readonly Dictionary<string, DiagnosticDescriptor> All =
        new[]
        {
            NotPartial, NotConstant, MissingToolName, NotStaticMethodGroup, SettingsNotRecord, DuplicateToolName,
            MissingDescription, SettingsAsParameter, DocumentationOff, UngeneratedReference, NotLowerSnakeCase
        }.ToDictionary(descriptor => descriptor.Id);

    public static DiagnosticDescriptor ById(string id) => All[id];

    private static DiagnosticDescriptor Error(string id, string title, string message) =>
        new(id, title, message, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static DiagnosticDescriptor Warning(string id, string title, string message) =>
        new(id, title, message, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);
}
