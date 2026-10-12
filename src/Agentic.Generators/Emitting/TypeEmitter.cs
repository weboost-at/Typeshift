using System;
using System.Collections.Generic;
using Agentic.Generators.Models;

namespace Agentic.Generators.Emitting;

internal static class TypeEmitter
{
    public static string EmitPartial(
        TypeModel type,
        IEnumerable<string> attributes,
        Action<SourceWriter> writeMembers,
        Action<SourceWriter>? writeAfter = null)
    {
        var writer = new SourceWriter();
        writer.Line(SourceWriter.Header);

        if (type.Namespace is not null) writer.Line($"namespace {type.Namespace};").Line();

        var blocks = new Stack<IDisposable>();

        foreach (var container in type.ContainingTypes)
        {
            blocks.Push(writer.Block($"partial {container}"));
        }

        foreach (var attribute in attributes)
        {
            writer.Line($"[{attribute}]");
        }

        using (writer.Block($"partial class {type.Name}"))
        {
            writeMembers(writer);
        }

        while (blocks.Count > 0)
        {
            blocks.Pop().Dispose();
        }

        writeAfter?.Invoke(writer);
        return writer.ToString();
    }
}
