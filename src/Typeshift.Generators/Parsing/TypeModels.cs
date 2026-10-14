using System.Collections.Generic;
using Typeshift.Generators.Emitting;
using Typeshift.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Typeshift.Generators.Parsing;

internal static class TypeModels
{
    private static readonly SymbolDisplayFormat Namespace = SymbolDisplayFormat.FullyQualifiedFormat
        .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

    public static TypeModel From(INamedTypeSymbol type)
    {
        var containing = new List<string>();

        for (var current = type.ContainingType; current is not null; current = current.ContainingType)
        {
            containing.Insert(0, $"{Keyword(current)} {Names.Identifier(current.Name)}");
        }

        return new TypeModel(
            Namespace: type.ContainingNamespace.IsGlobalNamespace
                ? null
                : type.ContainingNamespace.ToDisplayString(Namespace),
            Name: Names.Identifier(type.Name),
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
