using System.Linq;
using Agentic.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Agentic.Generators.Parsing;

// The member types settings can store: what a UI edits and StorableJson writes without any JSON contract
internal static class Storage
{
    public static StorageModel? Of(ITypeSymbol type)
    {
        var underlying =
            type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
                ? nullable.TypeArguments[0]
                : type;

        if (underlying.TypeKind is TypeKind.Enum)
            return new StorageModel("WriteEnum", $"ReadEnum<{underlying.ToTypeOf()}>");

        if (IsStringList(underlying)) return new StorageModel("Write", "ReadStrings");

        var read = underlying.SpecialType switch
        {
            SpecialType.System_String => "ReadString",
            SpecialType.System_Boolean => "ReadBoolean",
            SpecialType.System_Byte => "ReadByte",
            SpecialType.System_Int16 => "ReadInt16",
            SpecialType.System_Int32 => "ReadInt32",
            SpecialType.System_Int64 => "ReadInt64",
            SpecialType.System_Single => "ReadSingle",
            SpecialType.System_Double => "ReadDouble",
            SpecialType.System_Decimal => "ReadDecimal",
            SpecialType.System_DateTime => "ReadDateTime",
            _ when underlying.Is("System.Guid") => "ReadGuid",
            _ when underlying.Is("System.DateTimeOffset") => "ReadDateTimeOffset",
            _ when underlying.Is("System.TimeSpan") => "ReadTimeSpan",
            _ => null
        };

        return read is null ? null : new StorageModel("Write", read);
    }

    // ReadStrings returns a string[], which every one of these accepts
    private static bool IsStringList(ITypeSymbol type) =>
        type switch
        {
            IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String } => true,
            INamedTypeSymbol { TypeArguments.Length: 1 } named
                when named.TypeArguments[0].SpecialType is SpecialType.System_String =>
                new[]
                {
                    "System.Collections.Generic.IEnumerable`1",
                    "System.Collections.Generic.IReadOnlyCollection`1",
                    "System.Collections.Generic.IReadOnlyList`1",
                    "System.Collections.Generic.ICollection`1",
                    "System.Collections.Generic.IList`1"
                }.Any(named.Is),
            _ => false
        };
}
