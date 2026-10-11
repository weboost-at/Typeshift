namespace Agentic.Generators.Emitting;

internal static class Docs
{
    public static void WriteSummary(SourceWriter writer, string text)
    {
        writer.Line("/// <summary>");
        writer.Line($"/// {text}");
        writer.Line("/// </summary>");
    }

    public static void WriteInheritDoc(SourceWriter writer) => writer.Line("/// <inheritdoc />");
}
