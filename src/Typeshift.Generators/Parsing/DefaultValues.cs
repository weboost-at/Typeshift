using System;
using System.Globalization;
using System.Linq;
using Typeshift.Generators.Emitting;
using Typeshift.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Typeshift.Generators.Parsing;

// A parameter's default as a C# expression that boxes as the parameter's own type
internal static class DefaultValues
{
    public static string Format(IParameterSymbol parameter)
    {
        var type = parameter.Type;
        var isNullable = type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        var underlying = isNullable ? ((INamedTypeSymbol)type).TypeArguments[0] : type;

        // C# encodes a struct's "= default" as a null constant, which must stay a boxed default(T)
        return parameter.ExplicitDefaultValue switch
        {
            null when type.IsValueType && !isNullable => $"default({type.ToTypeOf()})",
            null when !type.IsValueType && type.NullableAnnotation != NullableAnnotation.Annotated =>
                ParameterModel.ForgivenNull,
            null => "null",
            var value when underlying.TypeKind == TypeKind.Enum => FormatEnum((INamedTypeSymbol)underlying, value),
            var value => FormatPrimitive(value)
        };
    }

    private static string FormatEnum(INamedTypeSymbol type, object value)
    {
        var member = type.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(field => field.HasConstantValue && Equals(field.ConstantValue, value));
        var name = type.ToTypeOf();

        // A negative value cast without parentheses would parse as a subtraction
        return member is null ? $"({name})({FormatPrimitive(value)})" : $"{name}.{Names.Identifier(member.Name)}";
    }

    private static string FormatPrimitive(object value) =>
        value switch
        {
            string text => SymbolDisplay.FormatLiteral(text, quote: true),
            char character => SymbolDisplay.FormatLiteral(character, quote: true),
            bool flag => flag ? "true" : "false",
            float number => FormatFloat(number),
            double number => FormatDouble(number),
            decimal number => number.ToString(CultureInfo.InvariantCulture) + "m",
            int number => number.ToString(CultureInfo.InvariantCulture),
            long number => number.ToString(CultureInfo.InvariantCulture) + "L",
            uint number => number.ToString(CultureInfo.InvariantCulture) + "U",
            ulong number => number.ToString(CultureInfo.InvariantCulture) + "UL",
            short number => $"(short){number.ToString(CultureInfo.InvariantCulture)}",
            ushort number => $"(ushort){number.ToString(CultureInfo.InvariantCulture)}",
            byte number => $"(byte){number.ToString(CultureInfo.InvariantCulture)}",
            sbyte number => $"(sbyte){number.ToString(CultureInfo.InvariantCulture)}",

            // [DateTimeConstant] declares the one default that is no literal
            DateTime time => $"new global::System.DateTime({time.Ticks.ToString(CultureInfo.InvariantCulture)}L)",
            _ => "default!"
        };

    private static string FormatFloat(float number) =>
        number switch
        {
            float.NaN => "float.NaN",
            float.PositiveInfinity => "float.PositiveInfinity",
            float.NegativeInfinity => "float.NegativeInfinity",
            _ => number.ToString("R", CultureInfo.InvariantCulture) + "f"
        };

    private static string FormatDouble(double number) =>
        number switch
        {
            double.NaN => "double.NaN",
            double.PositiveInfinity => "double.PositiveInfinity",
            double.NegativeInfinity => "double.NegativeInfinity",
            _ => number.ToString("R", CultureInfo.InvariantCulture) + "d"
        };
}
