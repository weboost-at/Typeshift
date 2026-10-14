using Microsoft.CodeAnalysis;

namespace Typeshift.Generators.Parsing;

// D2: the analyzer reports through it; the generator parses with None and skips what does not parse
internal sealed class Reporter(System.Action<Diagnostic>? report)
{
    public static readonly Reporter None = new(null);

    public void Report(DiagnosticDescriptor descriptor, Location? location, params object?[] arguments) =>
        report?.Invoke(Diagnostic.Create(descriptor, location, arguments));

    public void Report(DiagnosticDescriptor descriptor, SyntaxNode node, params object?[] arguments) =>
        Report(descriptor, node.GetLocation(), arguments);
}
