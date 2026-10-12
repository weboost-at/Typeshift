using System.Collections.Generic;
using System.Linq;
using Agentic.Generators.Models;

namespace Agentic.Generators.Emitting;

// The Serialize and Deserialize of a generated IStorable record, member by member through StorableJson
internal static class StorableEmitter
{
    public const string Json = "global::Agentic.Persistence.StorableJson";

    public static void WriteSerialize(
        SourceWriter writer,
        IReadOnlyList<StoredMember> members,
        IReadOnlyList<StoredTool> tools)
    {
        Docs.WriteInheritDoc(writer);
        writer.Line("public string Serialize() =>");

        using (writer.Indent())
        {
            if (members.Count == 0 && tools.Count == 0)
            {
                writer.Line($"{Json}.Write(static _ => {{ }});");
                return;
            }

            writer.Line($"{Json}.Write(writer =>");

            using (writer.Block(close: "});"))
            {
                foreach (var member in members)
                {
                    var name = Names.Literal(member.JsonName);
                    writer.Line($"{Json}.{member.Storage.Write}(writer, {name}, {Names.Identifier(member.Property)});");
                }

                if (tools.Count == 0) return;

                writer.Line($"{Json}.WriteObject(writer, \"tools\", tools =>");

                using (writer.Block(close: "});"))
                {
                    foreach (var tool in tools)
                    {
                        writer.Line($"{Json}.WriteStorable(tools, {Names.Literal(tool.Name)}, {tool.Property});");
                    }
                }
            }
        }
    }

    public static void WriteDeserialize(
        SourceWriter writer,
        string type,
        IReadOnlyList<StoredMember> members,
        IReadOnlyList<StoredTool> tools)
    {
        Docs.WriteInheritDoc(writer);
        writer.Line($"public static {type} Deserialize(string json) =>");

        using (writer.Indent())
        {
            if (members.Count == 0 && tools.Count == 0)
            {
                writer.Line($"{Json}.Read(json, static _ => new {type}());");
                return;
            }

            var assignments = members
                .Select(member => $"{Names.Identifier(member.Property)} = "
                    + $"{Json}.{member.Storage.Read}(data, {Names.Literal(member.JsonName)})")
                .Concat(tools.Select(tool =>
                    $"{tool.Property} = {Json}.ReadStorable<{tool.Type}>(tools, {Names.Literal(tool.Name)}) ?? new()"))
                .ToList();

            if (tools.Count == 0)
            {
                writer.Line($"{Json}.Read(json, static data => new {type}");
                WriteAssignments(writer, assignments, close: "});");
                return;
            }

            writer.Line($"{Json}.Read(json, static data =>");

            using (writer.Block(close: "});"))
            {
                writer.Line($"var tools = {Json}.ReadObject(data, \"tools\");");
                writer.Line();
                writer.Line($"return new {type}");
                WriteAssignments(writer, assignments, close: "};");
            }
        }
    }

    private static void WriteAssignments(SourceWriter writer, IReadOnlyList<string> assignments, string close)
    {
        using (writer.Block(close: close))
        {
            for (var index = 0; index < assignments.Count; index++)
            {
                writer.Line(assignments[index] + (index < assignments.Count - 1 ? "," : ""));
            }
        }
    }
}

// A member stored under JsonName; Property is the C# property, which a reserved name may have suffixed
internal sealed record StoredMember(string Property, string JsonName, StorageModel Storage);

// The overrides of a tool, stored under the tool's name in "tools"
internal sealed record StoredTool(string Property, string Name, string Type);
