using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Typeshift.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Typeshift.Generators.Diagnostics;

// Checks across everything the compilation sees: its own toolsets plus the stamps of its references
internal static class RegistrationDiagnostics
{
    // Reported once per name, on an own tool, or on AddTypeshift() when the clash is only among references
    public static IEnumerable<Diagnostic> GetDuplicateTools(
        ImmutableArray<ToolsetSummary> own,
        ReferenceModel references,
        ImmutableArray<InterceptModel> calls)
    {
        var duplicates = own
            .Concat(references.Modules.SelectMany(module => module.Toolsets))
            .SelectMany(toolset => toolset.Tools.Select(tool =>
                (tool.Name, DeclaredBy: toolset.Type.Replace("global::", ""), tool.Location)))
            .GroupBy(declaration => declaration.Name)
            .Where(group => group.Count() > 1);

        foreach (var duplicate in duplicates)
        {
            var location = duplicate
                .Select(declaration => declaration.Location)
                .FirstOrDefault(found => found is not null) ?? calls.FirstOrDefault()?.Location;

            if (location is null) continue;

            var declaredBy = string.Join(" and ", duplicate.Select(declaration => declaration.DeclaredBy));
            yield return Diagnostic.Create(
                Descriptors.DuplicateToolName,
                location.ToLocation(),
                duplicate.Key,
                declaredBy);
        }
    }

    // Only the host, the project that calls AddTypeshift(), misses what an ungenerated reference declares
    public static IEnumerable<Diagnostic> GetUngeneratedReferences(
        ReferenceModel references,
        ImmutableArray<InterceptModel> calls) =>
        calls.IsEmpty
            ? []
            : references.UngeneratedAssemblies.Select(assembly =>
                Diagnostic.Create(Descriptors.UngeneratedReference, calls[0].Location?.ToLocation(), assembly));
}
