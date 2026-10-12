using System.Collections.Generic;
using Agentic.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Agentic.Generators.Parsing;

internal static class TypeModels
{
    public static TypeModel From(INamedTypeSymbol type)
    {
        var containing = new List<string>();

        for (var current = type.ContainingType; current is not null; current = current.ContainingType)
        {
            containing.Insert(0, $"{Keyword(current)} {current.Name}");
        }

        return new TypeModel(
            Namespace: type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(),
            Name: type.Name,
            FullName: type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Accessibility: type.IsEffectivelyPublic() ? "public" : "internal",
            ContainingTypes: containing.ToEquatableArray());
    }

    private static string Keyword(INamedTypeSymbol type) =>
        (type.IsRecord, type.TypeKind) switch
        {
            (true, TypeKind.Struct) => "record struct",
            (true, _) => "record",
            (_, TypeKind.Struct) => "struct",
            (_, TypeKind.Interface) => "interface",
            _ => "class"
        };
}
