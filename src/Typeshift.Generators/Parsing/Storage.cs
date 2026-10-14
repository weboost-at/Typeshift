using System.Collections.Generic;
using System.Linq;
using Typeshift.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Typeshift.Generators.Parsing;

// D19: the types settings can store, which StorableJson and the generated helpers write without reflection; null for
// any other, TSH012
internal static class Storage
{
    private static readonly string[] Scalars =
    [
        "System.Guid", "System.DateTime", "System.DateTimeOffset", "System.TimeSpan", "System.DateOnly",
        "System.TimeOnly", "System.Uri", "Microsoft.Extensions.AI.ReasoningOptions"
    ];

    private static readonly string[] Lists =
    [
        "System.Collections.Generic.List`1", "System.Collections.Generic.IList`1",
        "System.Collections.Generic.IReadOnlyList`1", "System.Collections.Generic.ICollection`1",
        "System.Collections.Generic.IReadOnlyCollection`1", "System.Collections.Generic.IEnumerable`1"
    ];

    private static readonly string[] Sets =
    [
        "System.Collections.Generic.HashSet`1", "System.Collections.Generic.ISet`1",
        "System.Collections.Generic.IReadOnlySet`1"
    ];

    private static readonly string[] Maps =
    [
        "System.Collections.Generic.Dictionary`2", "System.Collections.Generic.IDictionary`2",
        "System.Collections.Generic.IReadOnlyDictionary`2"
    ];

    public static StorageModel? Of(ITypeSymbol type) =>
        Of(type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));

    // Visiting holds the objects being read, so a type that contains itself is refused instead of recursing
    private static StorageModel? Of(ITypeSymbol type, HashSet<ITypeSymbol> visiting)
    {
        var underlying = type is INamedTypeSymbol
        {
            OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
        } nullable
            ? nullable.TypeArguments[0]
            : type;

        var name = underlying.ToTypeOf();

        if (underlying.SpecialType is SpecialType.System_String or SpecialType.System_Boolean
            || Scalars.Any(underlying.Is))
            return new StorageModel(StorageKind.Scalar, name);

        if (underlying.SpecialType is SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_Int32
            or SpecialType.System_Int64 or SpecialType.System_Single or SpecialType.System_Double
            or SpecialType.System_Decimal)
            return new StorageModel(StorageKind.Number, name);

        if (underlying.TypeKind is TypeKind.Enum) return new StorageModel(StorageKind.Enum, name);

        if (underlying is IArrayTypeSymbol { Rank: 1, ElementType: var item })
            return Collection(StorageKind.Array, name, item, visiting);

        if (underlying is not INamedTypeSymbol named) return null;

        if (named.TypeArguments.Length == 1 && Lists.Any(named.Is))
            return Collection(StorageKind.List, name, named.TypeArguments[0], visiting);

        if (named.TypeArguments.Length == 1 && Sets.Any(named.Is))
            return Collection(StorageKind.Set, name, named.TypeArguments[0], visiting);

        if (named.TypeArguments.Length == 2 && Maps.Any(named.Is)) return Map(name, named, visiting);

        return Object(name, named, visiting);
    }

    // Items are values: a nullable value type item has no stored form
    private static StorageModel? Collection(
        StorageKind kind,
        string name,
        ITypeSymbol item,
        HashSet<ITypeSymbol> visiting) =>
        item.OriginalDefinition.SpecialType is not SpecialType.System_Nullable_T && Of(item, visiting) is { } element
            ? new StorageModel(kind, name, Element: element)
            : null;

    private static StorageModel? Map(string name, INamedTypeSymbol map, HashSet<ITypeSymbol> visiting)
    {
        var key = map.TypeArguments[0];
        var value = map.TypeArguments[1];

        if (value.OriginalDefinition.SpecialType is SpecialType.System_Nullable_T
            || Of(value, visiting) is not { } element) return null;

        if (key.SpecialType is SpecialType.System_String)
            return new StorageModel(StorageKind.Map, name, Element: element);

        return key.TypeKind is TypeKind.Enum
            ? new StorageModel(StorageKind.EnumMap, name, Element: element, Key: key.ToTypeOf())
            : null;
    }

    // A class, records included, created with its parameterless constructor and every public member set: replaced as a
    // whole, never merged
    private static StorageModel? Object(string name, INamedTypeSymbol type, HashSet<ITypeSymbol> visiting)
    {
        if (type is not { TypeKind: TypeKind.Class, IsAbstract: false, IsGenericType: false }
            || !type.InstanceConstructors.Any(constructor => constructor is
            {
                Parameters.IsEmpty: true,
                DeclaredAccessibility: Accessibility.Public
            })
            || HasRequiredMembers(type)
            || !visiting.Add(type)) return null;

        var members = new List<StoredMember>();

        // A member without a setter is computed from the others, an obsolete one no longer stored
        var settable = type.GetPublicProperties().Where(property => property.IsSettable && !property.IsObsolete);

        foreach (var property in settable)
        {
            if (Of(property.Type, visiting) is not { } storage)
            {
                visiting.Remove(type);
                return null;
            }

            members.Add(new StoredMember(property.Name, JsonName(property.Name), storage));
        }

        visiting.Remove(type);
        return new StorageModel(StorageKind.Object, name, members.ToEquatableArray());
    }

    // D44: camelCase
    public static string JsonName(string property) =>
        char.ToLowerInvariant(property[0]) + property.Substring(1);

    // new() cannot create a class that requires members be set
    private static bool HasRequiredMembers(INamedTypeSymbol type)
    {
        for (var level = type; level is not null; level = level.BaseType)
        {
            if (level.GetMembers().Any(member => member is IPropertySymbol { IsRequired: true }
                    or IFieldSymbol { IsRequired: true }))
                return true;
        }

        return false;
    }
}
