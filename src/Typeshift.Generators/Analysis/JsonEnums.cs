using System.Collections.Generic;
using System.Linq;
using Typeshift.Generators.Parsing;
using Microsoft.CodeAnalysis;

namespace Typeshift.Generators.Analysis;

// D45: where JSON reflection is on, Microsoft.Extensions.AI's defaults write every enum as its name. Where it is off
// the JSON context decides, and unless it says otherwise an enum is a number there
internal static class JsonEnums
{
    private const string Options = "System.Text.Json.Serialization.JsonSourceGenerationOptionsAttribute";

    private const string Converter = "System.Text.Json.Serialization.JsonConverterAttribute";

    private const string Ignore = "System.Text.Json.Serialization.JsonIgnoreAttribute";

    // [JsonSourceGenerationOptions(UseStringEnumConverter = true)]
    public static bool AreNames(INamedTypeSymbol context) =>
        context.GetAttributes()
            .Where(attribute => attribute.AttributeClass.Is(Options))
            .SelectMany(attribute => attribute.NamedArguments)
            .Any(argument => argument is { Key: "UseStringEnumConverter", Value.Value: true });

    // The types the context's own converters write: Converters = [typeof(JsonStringEnumConverter<Mode>)]
    public static IReadOnlyList<ISymbol> GetConverted(INamedTypeSymbol context) =>
        context.GetAttributes()
            .Where(attribute => attribute.AttributeClass.Is(Options))
            .SelectMany(attribute => attribute.NamedArguments)
            .Where(argument => argument is { Key: "Converters", Value.Kind: TypedConstantKind.Array })
            .SelectMany(argument => argument.Value.Values)
            .Select(converter => converter.Value)
            .OfType<INamedTypeSymbol>()
            .SelectMany(converter => converter.TypeArguments)
            .ToList();

    // The first enum in a type's JSON that no converter writes, neither one of its own nor one of the context's: the
    // type itself, a type argument, an array's element or the type of a property, however deep
    public static INamedTypeSymbol? FindNumeric(ITypeSymbol type, IReadOnlyList<ISymbol> converted) =>
        Find(type, new HashSet<ISymbol>(converted, SymbolEqualityComparer.Default));

    private static INamedTypeSymbol? Find(ITypeSymbol type, HashSet<ISymbol> seen)
    {
        if (type is IArrayTypeSymbol array) return Find(array.ElementType, seen);
        if (type is not INamedTypeSymbol named || !seen.Add(named) || Has(named, Converter)) return null;
        if (named.TypeKind == TypeKind.Enum) return named;

        // A type of the framework has a converter of the serializer's own, as DateTime has, whose DayOfWeek is an enum
        var members = IsFramework(named)
            ? []
            : named.GetPublicProperties()
                .Where(property => !Has(property, Converter) && !Has(property, Ignore))
                .Select(property => property.Type);

        return named.TypeArguments
            .Concat(members)
            .Select(member => Find(member, seen))
            .FirstOrDefault(found => found is not null);
    }

    private static bool Has(ISymbol symbol, string attribute) =>
        symbol.GetAttributes().Any(applied => applied.AttributeClass.Is(attribute));

    private static bool IsFramework(INamedTypeSymbol type)
    {
        var root = type.ContainingNamespace;

        while (root is { IsGlobalNamespace: false, ContainingNamespace.IsGlobalNamespace: false })
        {
            root = root.ContainingNamespace;
        }

        return root is { Name: "System" };
    }
}
