using System;
using System.Collections.Generic;

namespace Typeshift.Generators.Emitting;

internal static class Docs
{
    public static void WriteSummary(SourceWriter writer, string text) => WriteTag(writer, "summary", "summary", text);

    public static void WriteTypeParam(SourceWriter writer, string name, string text) =>
        WriteTag(writer, $"typeparam name=\"{name}\"", "typeparam", text);

    public static void WriteParam(SourceWriter writer, string name, string text) =>
        WriteTag(writer, $"param name=\"{name}\"", "param", text);

    public static void WriteReturns(SourceWriter writer, string text) => WriteTag(writer, "returns", "returns", text);

    public static void WriteException(SourceWriter writer, string type, string text) =>
        WriteTag(writer, $"exception cref=\"{type}\"", "exception", text);

    public static void WriteInheritDoc(SourceWriter writer) => writer.Line("/// <inheritdoc />");

    // Docs and descriptions are read as plain text; put back into XML they need escaping
    public static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    // A summary always on lines of its own, any other tag on one line while it fits the width the writer has left
    private static void WriteTag(SourceWriter writer, string open, string close, string text)
    {
        // A description may span lines, a doc comment line may not
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var single = $"/// <{open}>{string.Join(" ", words)}</{close}>";

        if (close != "summary" && single.Length <= writer.Width)
        {
            writer.Line(single);
            return;
        }

        writer.Line($"/// <{open}>");

        foreach (var line in Wrap(words, writer.Width - 4))
        {
            writer.Line($"/// {line}");
        }

        writer.Line($"/// </{close}>");
    }

    private static IEnumerable<string> Wrap(IEnumerable<string> words, int width)
    {
        var line = "";

        foreach (var word in words)
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                yield return line;
                line = "";
            }

            line = line.Length == 0 ? word : $"{line} {word}";
        }

        yield return line;
    }
}
