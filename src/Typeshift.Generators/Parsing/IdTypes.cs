using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Typeshift.Generators.Models;

namespace Typeshift.Generators.Parsing;

// D56: the build's provider id type, from the TypeshiftProviderIdType property the package's props pass to the
// compiler; settings and conversation ids are typed in code (D72)
internal static class IdTypes
{
    public const string ProviderName = "TypeshiftProviderIdType";

    public const string ProviderProperty = "build_property." + ProviderName;

    public static string? Read(AnalyzerConfigOptions options) =>
        options.TryGetValue(ProviderProperty, out var value) && value.Trim().Length > 0 ? value.Trim() : null;

    // A C# keyword, a full name or a name in System; string when the property is not set, as outside an SDK build.
    // Null for a name the build cannot find, which is TSH029
    public static ITypeSymbol? Resolve(string? property, Compilation compilation)
    {
        var name = property ?? "string";

        var special = name switch
        {
            "bool" => SpecialType.System_Boolean,
            "byte" => SpecialType.System_Byte,
            "sbyte" => SpecialType.System_SByte,
            "char" => SpecialType.System_Char,
            "short" => SpecialType.System_Int16,
            "ushort" => SpecialType.System_UInt16,
            "int" => SpecialType.System_Int32,
            "uint" => SpecialType.System_UInt32,
            "long" => SpecialType.System_Int64,
            "ulong" => SpecialType.System_UInt64,
            "float" => SpecialType.System_Single,
            "double" => SpecialType.System_Double,
            "decimal" => SpecialType.System_Decimal,
            "string" => SpecialType.System_String,
            _ => SpecialType.None
        };

        return special != SpecialType.None
            ? compilation.GetSpecialType(special)
            : compilation.GetTypeByMetadataName(name) ?? compilation.GetTypeByMetadataName($"System.{name}");
    }

    // A provider id is one value Settings store. Null for a name the build cannot find or a type stored otherwise,
    // which is TSH029
    public static ITypeSymbol? ResolveProvider(string? property, Compilation compilation) =>
        Resolve(property, compilation) is { } type
        && Storage.Of(type) is { Kind: StorageKind.Scalar or StorageKind.Number or StorageKind.Enum }
            ? type
            : null;

    // The generated code stays valid with a provider id type that cannot be used; TSH029 says what to fix
    public static ProviderIdModel Provider(AnalyzerConfigOptions options, Compilation compilation) =>
        ResolveProvider(Read(options), compilation) is { } type && Storage.Of(type) is { } storage
            ? new ProviderIdModel(type.ToTypeOf(), storage, type.IsEffectivelyPublic())
            : new ProviderIdModel("string", new StorageModel(StorageKind.Scalar, "string"), IsPublic: true);
}
