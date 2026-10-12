using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Typeshift.Generators.Models;

// Collects a model's diagnostics as equatable infos while the parser still holds the Roslyn locations
internal sealed class DiagnosticBag
{
    private readonly List<DiagnosticInfo> _diagnostics = [];

    public void Report(DiagnosticDescriptor descriptor, Location? location, params string[] arguments) =>
        _diagnostics.Add(DiagnosticInfo.Create(descriptor, LocationInfo.From(location), arguments));

    public void Report(DiagnosticDescriptor descriptor, SyntaxNode node, params string[] arguments) =>
        Report(descriptor, node.GetLocation(), arguments);

    public EquatableArray<DiagnosticInfo> ToEquatableArray() => _diagnostics.ToEquatableArray();
}
