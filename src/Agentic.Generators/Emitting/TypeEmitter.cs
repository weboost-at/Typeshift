using System;
using System.Collections.Generic;
using Agentic.Generators.Models;

namespace Agentic.Generators.Emitting;

// Reopens a partial class, inside its namespace and any containing types
internal static class TypeEmitter
{
    public static string EmitPartial(TypeModel type, Action<SourceWriter> writeMembers)
    {
        var writer = new SourceWriter();
        writer.Line(SourceWriter.Header);

        if (type.Namespace is not null) writer.Line($"namespace {type.Namespace};").Line();

        var blocks = new Stack<IDisposable>();

        foreach (var container in type.ContainingTypes)
        {
            blocks.Push(writer.Block($"partial {container}"));
        }

        using (writer.Block($"partial class {type.Name}"))
        {
            writeMembers(writer);
        }

        while (blocks.Count > 0)
        {
            blocks.Pop().Dispose();
        }

        return writer.ToString();
    }
}
