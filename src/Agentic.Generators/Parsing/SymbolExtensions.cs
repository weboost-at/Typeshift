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

    extension(ITypeSymbol? type)
    {
        public bool Is(string metadataName) =>
            type is INamedTypeSymbol named && GetMetadataName(named.OriginalDefinition) == metadataName;
    }

    extension(ITypeSymbol type)
    {
        public string ToFullyQualified() => type.ToDisplayString(FullyQualified);

        // Public along its containing types and type arguments, so other assemblies can name it
        public bool IsEffectivelyPublic() =>
            type switch
            {
                IArrayTypeSymbol array => array.ElementType.IsEffectivelyPublic(),
                INamedTypeSymbol named => named.DeclaredAccessibility == Accessibility.Public
                    && (named.ContainingType?.IsEffectivelyPublic() ?? true)
                    && named.TypeArguments.All(argument => argument.IsEffectivelyPublic()),
                _ => true
            };

        // The type of a stored value: null means "inherit", so even a value type becomes nullable
        public string ToNullableFullyQualified() =>
            type switch
            {
                { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } => type.ToFullyQualified(),
                { IsValueType: true } => type.ToFullyQualified() + "?",
                _ => type.WithNullableAnnotation(NullableAnnotation.Annotated).ToFullyQualified()
            };

        // typeof() and generic definitions take no nullable reference annotation
        public string ToTypeOf() =>
            type.WithNullableAnnotation(NullableAnnotation.NotAnnotated)
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        public IEnumerable<IPropertySymbol> GetSettableProperties()
        {
            var hierarchy = new List<ITypeSymbol>();

            for (var level = type; level is { SpecialType: not SpecialType.System_Object }; level = level.BaseType)
            {
                hierarchy.Insert(0, level);
            }

            // Base records first; a redeclared property counts once, as its most derived declaration
            return hierarchy
                .SelectMany(level => level.GetMembers().OfType<IPropertySymbol>())
                .Where(IsSettable)
                .GroupBy(property => property.Name)
                .Select(group => group.Last());
        }
    }

    extension(INamedTypeSymbol type)
    {
        public bool DerivesFrom(string metadataName)
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (current.Is(metadataName)) return true;
            }

            return false;
        }

        public bool IsPartial(CancellationToken ct) =>
            type.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax(ct))
                .OfType<TypeDeclarationSyntax>()
                .Any(declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            && (type.ContainingType is not { } container || container.IsPartial(ct));
    }

    private static bool IsSettable(IPropertySymbol property) =>
        property is { IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public }
        && property.SetMethod is { DeclaredAccessibility: Accessibility.Public };

    private static string GetMetadataName(INamedTypeSymbol type) =>
        type.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? $"{ns.ToDisplayString()}.{type.MetadataName}"
            : type.MetadataName;
}
