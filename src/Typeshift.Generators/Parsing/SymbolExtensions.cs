using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Typeshift.Generators.Parsing;

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

        // Internal or wider along its containing types, so code outside the class but in its assembly can name it
        public bool IsVisibleInAssembly() =>
            type.DeclaredAccessibility
                is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal
            && (type.ContainingType?.IsVisibleInAssembly() ?? true);

        // The type of an override: null means "the layer below", so even a value type becomes nullable
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

        // Base records first; a redeclared property counts once, as its most derived declaration
        public IEnumerable<IPropertySymbol> GetPublicProperties()
        {
            var hierarchy = new List<ITypeSymbol>();

            for (var level = type; level is { SpecialType: not SpecialType.System_Object }; level = level.BaseType)
            {
                hierarchy.Insert(0, level);
            }

            return hierarchy
                .SelectMany(level => level.GetMembers().OfType<IPropertySymbol>())
                .Where(property => property is
                {
                    IsStatic: false,
                    IsIndexer: false,
                    DeclaredAccessibility: Accessibility.Public,
                    Name: not "EqualityContract"
                })
                .GroupBy(property => property.Name)
                .Select(group => group.Last());
        }
    }

    extension(IPropertySymbol property)
    {
        public bool IsSettable => property.SetMethod is { DeclaredAccessibility: Accessibility.Public };

        public bool IsObsolete =>
            property.GetAttributes().Any(attribute => attribute.AttributeClass.Is(KnownTypes.Obsolete));
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

        // The type itself or the innermost class it is nested in that is not partial; null when all of them are
        public INamedTypeSymbol? GetNotPartial(CancellationToken ct) =>
            type.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax(ct))
                .OfType<TypeDeclarationSyntax>()
                .Any(declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                ? type.ContainingType?.GetNotPartial(ct)
                : type;
    }

    private static string GetMetadataName(INamedTypeSymbol type) =>
        type.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? $"{ns.ToDisplayString()}.{type.MetadataName}"
            : type.MetadataName;
}
