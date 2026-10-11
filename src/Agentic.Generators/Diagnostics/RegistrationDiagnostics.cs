using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Agentic.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Agentic.Generators.Diagnostics;

// Checks across everything the compilation sees: its own toolsets plus the stamps of its references
internal static class RegistrationDiagnostics
{
    // Reported once per name, on an own tool, or on AddAgentic() when the clash is only among references
    public static IEnumerable<Diagnostic> GetDuplicateTools(
        ImmutableArray<ToolsetSummary> own,
        ReferenceModel references,
        ImmutableArray<InterceptModel> calls)
    {
        var visible = own.Concat(references.Modules.SelectMany(module => module.Toolsets));

        var duplicates = visible
            .SelectMany(toolset => toolset.Tools.Select(tool => (Toolset: toolset.Type, Tool: tool)))
            .GroupBy(declaration => declaration.Tool.Name)
            .Where(group => group.Count() > 1);

        foreach (var duplicate in duplicates)
        {
            var location = duplicate
                .Select(declaration => declaration.Tool.Location)
                .FirstOrDefault(found => found is not null) ?? calls.FirstOrDefault()?.Location;

            if (location is null) continue;

            var declarations = duplicate.Select(declaration =>
                $"{declaration.Toolset.Replace("global::", "")}.{declaration.Tool.Method}");

            yield return Diagnostic.Create(
                Descriptors.DuplicateToolName,
                location.ToLocation(),
                duplicate.Key,
                string.Join(" and ", declarations));
        }
    }

    // Only the host, the project that calls AddAgentic(), misses what an ungenerated reference declares
    public static IEnumerable<Diagnostic> GetUngeneratedReferences(
        ReferenceModel references,
        ImmutableArray<InterceptModel> calls) =>
        calls.IsEmpty
            ? []
            : references.UngeneratedAssemblies.Select(assembly =>
                Diagnostic.Create(Descriptors.UngeneratedReference, calls[0].Location?.ToLocation(), assembly));
}
