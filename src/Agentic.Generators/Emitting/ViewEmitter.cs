namespace Agentic.Generators.Emitting;

// The members the generated configuration views share: a private constructor, Create, one property per value
internal static class ViewEmitter
{
    public const string SerializerOptions = "global::System.Text.Json.JsonSerializerOptions";

    // The view class with its base list, one base per line
    public static void WriteDeclaration(SourceWriter writer, string view, string baseView, string viewInterface)
    {
        writer.Line($"public sealed class {view} :");

        using (writer.Indent())
        {
            writer.Line($"{baseView},");
            writer.Line($"{viewInterface}<{view}>");
        }
    }

    public static void WriteConstructor(SourceWriter writer, string view, string storedType, string stored)
    {
        writer.Line($"private {view}(");

        using (writer.Indent())
        {
            writer.Line($"{storedType} {stored},");
            writer.Line($"{SerializerOptions} options)");
            writer.Line($": base({stored}, options)");
        }

        using (writer.Block())
        {
        }
    }

    public static void WriteCreate(SourceWriter writer, string view, string storedType, string stored)
    {
        Docs.WriteInheritDoc(writer);
        writer.Line($"public static {view} Create(");

        using (writer.Indent())
        {
            writer.Line($"{storedType} {stored},");
            writer.Line($"{SerializerOptions} options) =>");
            writer.Line($"new({stored}, options);");
        }
    }

    // A stored value under its member's name: null means the member inherits from the layer below
    public static void WriteValue(
        SourceWriter writer,
        string name,
        string property,
        string type,
        string get,
        string set)
    {
        Docs.WriteSummary(writer, $"The stored <c>{name}</c>; null inherits it from the layer below.");

        using (writer.Block($"public {type} {Names.Identifier(property)}"))
        {
            writer.Line($"get => {get}<{type}>({Names.Literal(name)});");
            writer.Line($"set => {set}({Names.Literal(name)}, value);");
        }
    }
}
