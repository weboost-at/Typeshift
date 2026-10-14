using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Typeshift.Generators.Parsing;

// D11: the build's id type, from the TypeshiftIdType property the package's props pass to the compiler
internal static class IdTypes
{
    public const string Property = "build_property.TypeshiftIdType";

    public static string? Read(AnalyzerConfigOptions options) =>
        options.TryGetValue(Property, out var value) && value.Trim().Length > 0 ? value.Trim() : null;

    // A C# keyword, a full name or a name in System; Guid when the property is not set, as outside an SDK build
    public static ITypeSymbol? Resolve(string? property, Compilation compilation)
    {
        var name = property ?? "Guid";

        var special = name switch
        {
            "int" => SpecialType.System_Int32,
            "long" => SpecialType.System_Int64,
            "short" => SpecialType.System_Int16,
            "string" => SpecialType.System_String,
            _ => SpecialType.None
        };

        return special != SpecialType.None
            ? compilation.GetSpecialType(special)
            : compilation.GetTypeByMetadataName(name) ?? compilation.GetTypeByMetadataName($"System.{name}");
    }

    public static string Display(string? property, Compilation compilation) =>
        Resolve(property, compilation)?.ToTypeOf() ?? property ?? "global::System.Guid";

    // How the raw path reads a conversation id from ChatOptions.ConversationId: a string as it is, a parsable type
    // through IParsable, any other not at all
    public static string Parser(string? property, Compilation compilation) =>
        Resolve(property, compilation) switch
        {
            { SpecialType: SpecialType.System_String } => "static text => text",
            { } type when type.AllInterfaces.Any(contract => contract.Is("System.IParsable`1")) =>
                $"static text => {type.ToTypeOf()}.Parse(text, "
                + "global::System.Globalization.CultureInfo.InvariantCulture)",
            _ => "null"
        };
}
