using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Agentic.Generators.Parsing;

internal static class SymbolExtensions
{
    public static readonly SymbolDisplayFormat FullyQualified = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static bool Is(this ITypeSymbol? type, string metadataName) =>
        type is INamedTypeSymbol named && GetMetadataName(named.OriginalDefinition) == metadataName;

    public static bool DerivesFrom(this INamedTypeSymbol type, string metadataName)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.Is(metadataName)) return true;
        }

        return false;
    }

    public static string ToFullyQualified(this ITypeSymbol type) => type.ToDisplayString(FullyQualified);

    // The type of a stored value: null means "inherit", so even a value type becomes nullable
    public static string ToNullableFullyQualified(this ITypeSymbol type) =>
        type switch
        {
            { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } => type.ToFullyQualified(),
            { IsValueType: true } => type.ToFullyQualified() + "?",
            _ => type.WithNullableAnnotation(NullableAnnotation.Annotated).ToFullyQualified()
        };

    // typeof() and generic definitions take no nullable reference annotation
    public static string ToTypeOf(this ITypeSymbol type) =>
        type.WithNullableAnnotation(NullableAnnotation.NotAnnotated)
            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    public static bool IsPartial(this INamedTypeSymbol type, CancellationToken ct) =>
        type.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(ct))
            .OfType<TypeDeclarationSyntax>()
            .Any(declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
        && (type.ContainingType is not { } container || container.IsPartial(ct));

    public static IEnumerable<IPropertySymbol> GetSettableProperties(this ITypeSymbol type)
    {
        var hierarchy = new List<ITypeSymbol>();

        for (var current = type; current is { SpecialType: not SpecialType.System_Object }; current = current.BaseType)
        {
            hierarchy.Insert(0, current);
        }

        // Base records first; a redeclared property counts once, as its most derived declaration
        return hierarchy
            .SelectMany(level => level.GetMembers().OfType<IPropertySymbol>())
            .Where(IsSettable)
            .GroupBy(property => property.Name)
            .Select(group => group.Last());
    }

    private static bool IsSettable(IPropertySymbol property) =>
        property is { IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public }
        && property.SetMethod is { DeclaredAccessibility: Accessibility.Public };

    private static string GetMetadataName(INamedTypeSymbol type) =>
        type.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? $"{ns.ToDisplayString()}.{type.MetadataName}"
            : type.MetadataName;
}
