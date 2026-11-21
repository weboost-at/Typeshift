using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Typeshift.Generators.Models;
using Typeshift.Generators.Naming;
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
        // The name alone is compared first, which builds no string: the analyzer asks this of every class and the
        // parsers of every attribute, and nearly all of them are named otherwise
        public bool Is(string metadataName) =>
            type is INamedTypeSymbol named
            && EndsInName(metadataName, named.Name)
            && GetMetadataName(named.OriginalDefinition) == metadataName;
    }

    extension(IEnumerable<ISymbol?> symbols)
    {
        // The ids of every [Experimental] that code naming the symbols reports, sorted, so a model holds them
        public EquatableArray<string> GetExperimentalIds()
        {
            List<string>? ids = null;

            foreach (var symbol in symbols)
            {
                ReadExperimentalIds(symbol, ref ids);
            }

            return ToExperimentalIds(ids);
        }
    }

    extension(ISymbol symbol)
    {
        public EquatableArray<string> GetExperimentalIds() => new[] { symbol }.GetExperimentalIds();
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

        // Internal or wider and in no file alone, along its containing types, its type arguments and an array's
        // elements, so code outside the class but in its assembly, in any file, can name it
        public bool IsVisibleInAssembly() =>
            type switch
            {
                IArrayTypeSymbol array => array.ElementType.IsVisibleInAssembly(),
                INamedTypeSymbol named => IsInternalOrWider(named)
                    && !named.IsFileLocal
                    && (named.ContainingType?.IsVisibleInAssembly() ?? true)
                    && named.TypeArguments.All(argument => argument.IsVisibleInAssembly()),
                _ => IsInternalOrWider(type)
            };

        // A file-local type, by itself, as a containing type, a type argument or an array's element: only its own
        // file can name it
        public bool ContainsFileLocalType() =>
            type switch
            {
                IArrayTypeSymbol array => array.ElementType.ContainsFileLocalType(),
                INamedTypeSymbol named => named.IsFileLocalOrNestedInOne()
                    || named.TypeArguments.Any(argument => argument.ContainsFileLocalType()),
                _ => false
            };

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

        // A type that does not resolve, by itself or as a type argument or an array's element: the compiler reports
        // it where it is written
        public bool ContainsErrorType() =>
            type switch
            {
                IArrayTypeSymbol array => array.ElementType.ContainsErrorType(),
                INamedTypeSymbol named => named.TypeKind == TypeKind.Error
                    || named.TypeArguments.Any(argument => argument.ContainsErrorType()),
                _ => false
            };

        // A type parameter, by itself, as a type argument, an array's element or of a containing type: a generic
        // type named without its type arguments, which only code still being typed does
        public bool ContainsTypeParameter() =>
            type switch
            {
                ITypeParameterSymbol => true,
                IArrayTypeSymbol array => array.ElementType.ContainsTypeParameter(),
                INamedTypeSymbol named => named.TypeArguments.Any(argument => argument.ContainsTypeParameter())
                    || (named.ContainingType?.ContainsTypeParameter() ?? false),
                _ => false
            };

        // What generated code names of a settings type: the type, its settable members and their types
        public EquatableArray<string> GetSettingsExperimentalIds()
        {
            List<string>? ids = null;
            ReadExperimentalIds(type, ref ids);

            foreach (var property in type.GetPublicProperties())
            {
                if (!property.IsSettable) continue;

                ReadExperimentalIds(property, ref ids);
                ReadExperimentalIds(property.Type, ref ids);
            }

            return ToExperimentalIds(ids);
        }

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
        public bool IsFileLocalOrNestedInOne() =>
            type.IsFileLocal || (type.ContainingType?.IsFileLocalOrNestedInOne() ?? false);

        // TSH016: the generated code outside the class names it, and the module creates it; null when it can. A
        // constructor with parameters is what a developer used to injection writes first, so that reason says where
        // services go
        public string? GetUnreachableReason() =>
            type.IsFileLocalOrNestedInOne() ? "it is file-local, and generated code is written to other files"
            : !type.IsVisibleInAssembly() ? "it is nested as private or protected"
            : !type.InstanceConstructors.Any(constructor => constructor is
            {
                Parameters.IsEmpty: true,
                DeclaredAccessibility:
                    Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal
            }) ? "it has no public or internal parameterless constructor, and Typeshift creates it with new; services "
                + "go elsewhere: a tool takes an IServiceProvider parameter, and an agent's context provider, "
                + "UseContext<T>(), is created from the run's services"
            : null;

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

    // A symbol's own ids, those of what contains it, and for a type those of its type arguments and of an array's
    // elements. D3: every edit reads them for each toolset and agent again, and nearly every symbol carries none, so
    // the list is made for the first id found
    private static void ReadExperimentalIds(ISymbol? symbol, ref List<string>? ids)
    {
        switch (symbol)
        {
            // No attribute marks a type parameter, and what contains it holds it among its own type arguments, so
            // reading on would never end. One gets here in AddTool(Finder<>.Find), while the type argument is typed
            case null or ITypeParameterSymbol:
                return;

            case IArrayTypeSymbol array:
                ReadExperimentalIds(array.ElementType, ref ids);
                return;

            case INamedTypeSymbol named:
                foreach (var argument in named.TypeArguments)
                {
                    ReadExperimentalIds(argument, ref ids);
                }

                break;
        }

        ReadOwnExperimentalIds(symbol, ref ids);

        if (symbol.ContainingType is { } type)
        {
            ReadExperimentalIds(type, ref ids);
            return;
        }

        ReadOwnExperimentalIds(symbol.ContainingModule, ref ids);
        ReadOwnExperimentalIds(symbol.ContainingAssembly, ref ids);
    }

    // An id is an identifier, which the compiler checks; one read from metadata goes into a #pragma, so it is checked
    private static void ReadOwnExperimentalIds(ISymbol? symbol, ref List<string>? ids)
    {
        if (symbol is null) return;

        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass.Is(KnownTypes.Experimental)
                && attribute.ConstructorArguments.Length > 0
                && attribute.ConstructorArguments[0] is { Kind: TypedConstantKind.Primitive, Value: string id }
                && SyntaxFacts.IsValidIdentifier(id))
                (ids ??= []).Add(id);
        }
    }

    private static EquatableArray<string> ToExperimentalIds(List<string>? ids) =>
        ids is null ? default : ids.Distinct().OrderBy(id => id, StringComparer.Ordinal).ToEquatableArray();

    private static bool IsInternalOrWider(ITypeSymbol type) =>
        type.DeclaredAccessibility
            is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;

    // Typeshift.ToolBuilder`1 ends in the name ToolBuilder
    private static bool EndsInName(string metadataName, string name)
    {
        var start = metadataName.LastIndexOf('.') + 1;
        var end = start + name.Length;

        return end <= metadataName.Length
            && string.CompareOrdinal(metadataName, start, name, 0, name.Length) == 0
            && (end == metadataName.Length || metadataName[end] == '`');
    }

    private static string GetMetadataName(INamedTypeSymbol type) =>
        type.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? $"{ns.ToDisplayString()}.{type.MetadataName}"
            : type.MetadataName;
}
