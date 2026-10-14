using System.Collections.Generic;
using System.Linq;
using Typeshift.Generators.Models;

namespace Typeshift.Generators.Emitting;

// D44: the JSON of one generated record, member by member. Scalars go through StorableJson; every other type gets a
// private WriteStored{n} and TryReadStored{n} in the record, which the members and the other helpers call
internal sealed class StorageEmitter
{
    public const string Json = "global::Typeshift.Generated.StorableJson";

    private const string Writer = "global::System.Text.Json.Utf8JsonWriter";

    private const string Element = "global::System.Text.Json.JsonElement";

    private const string ObjectKind = "global::System.Text.Json.JsonValueKind.Object";

    private const string Hidden =
        "[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]";

    private readonly List<StorageModel> _helpers = [];

    // WriteJson and TryReadJson of an IJsonStorable record; writeMore and readMore add what is not a stored member
    public static void WriteRecordJson(
        SourceWriter writer,
        string type,
        IEnumerable<(string Property, string JsonName, StorageModel Storage)> members,
        System.Action<SourceWriter>? writeMore = null,
        IReadOnlyList<string>? readMore = null)
    {
        var storage = new StorageEmitter();
        var list = members.ToList();

        writer.Line();
        Docs.WriteInheritDoc(writer);
        writer.Line(Hidden);

        using (writer.Block("public void WriteJson(global::System.Text.Json.Utf8JsonWriter writer)"))
        {
            writer.Line("writer.WriteStartObject();");
            writer.Line();
            storage.WriteMembers(writer, list);

            if (writeMore is not null)
            {
                if (list.Count > 0) writer.Line();
                writeMore(writer);
            }

            writer.Line();
            writer.Line("writer.WriteEndObject();");
        }

        writer.Line();
        Docs.WriteInheritDoc(writer);
        writer.Line(Hidden);
        writer.Line($"public static bool TryReadJson(global::System.Text.Json.JsonElement element, out {type} value)");

        using (writer.Block())
        {
            writer.Line("value = null!;");
            writer.Line("if (element.ValueKind is not global::System.Text.Json.JsonValueKind.Object) return false;");
            writer.Line();

            var assignments = storage.ReadMembers(list, "element", _ => "null").Concat(readMore ?? []).ToList();

            if (assignments.Count == 0)
            {
                writer.Line("value = new();");
            }
            else
            {
                writer.Line("value = new()");

                using (writer.Block(close: "};"))
                {
                    writer.List(assignments);
                }
            }

            writer.Line();
            writer.Line("return true;");
        }

        storage.WriteHelpers(writer);
    }


    // A statement that writes value
    public string Write(StorageModel model, string writer, string value) =>
        model.Kind switch
        {
            StorageKind.Scalar => $"{Json}.Write({writer}, {value});",
            StorageKind.Number => $"{Json}.WriteNumber({writer}, {value});",
            StorageKind.Enum => $"{Json}.WriteEnum({writer}, {value});",
            _ => $"WriteStored{Helper(model)}({writer}, {value});"
        };

    // A Boolean expression that reads element into a new variable target
    public string Read(StorageModel model, string element, string target) =>
        $"{Reader(model)}({element}, out {model.Type} {target})";

    // The members a record writes as a JSON object, each left out when null; the caller opens and closes the object
    public void WriteMembers(
        SourceWriter writer,
        IEnumerable<(string Property, string JsonName, StorageModel Storage)> members)
    {
        var index = 0;

        foreach (var (property, jsonName, storage) in members)
        {
            var local = $"member{index++}";

            if (index > 1) writer.Line();

            using (writer.Block($"if ({property} is {{ }} {local})"))
            {
                writer.Line($"writer.WritePropertyName({Names.Literal(jsonName)});");
                writer.Line(Write(storage, "writer", local));
            }
        }
    }

    // The assignments of an object initializer that reads each member, or takes fallback(property) when it does not
    // read
    public IReadOnlyList<string> ReadMembers(
        IEnumerable<(string Property, string JsonName, StorageModel Storage)> members,
        string element,
        System.Func<string, string> fallback) =>
    [
        .. members.Select((member, index) =>
            $"{member.Property} = "
            + Read(member.Storage, $"{Json}.Member({element}, {Names.Literal(member.JsonName)})", $"member{index}")
            + $" ? member{index} : {fallback(member.Property)}")
    ];

    // The helpers the record's members asked for, and those the helpers ask for in turn
    public void WriteHelpers(SourceWriter writer)
    {
        for (var index = 0; index < _helpers.Count; index++)
        {
            var model = _helpers[index];

            writer.Line();

            using (writer.Block($"private static void WriteStored{index}({Writer} writer, {model.Type} value)"))
            {
                WriteValue(writer, model);
            }

            writer.Line();

            using (writer.Block($"private static bool TryReadStored{index}({Element} element, out {model.Type} value)"))
            {
                ReadValue(writer, model);
            }
        }
    }

    private string Reader(StorageModel model) =>
        model.Kind switch
        {
            StorageKind.Scalar => $"{Json}.TryRead",
            StorageKind.Number => $"{Json}.TryReadNumber<{model.Type}>",
            StorageKind.Enum => $"{Json}.TryReadEnum<{model.Type}>",
            _ => $"TryReadStored{Helper(model)}"
        };

    private int Helper(StorageModel model)
    {
        var index = _helpers.IndexOf(model);
        if (index >= 0) return index;

        _helpers.Add(model);
        return _helpers.Count - 1;
    }

    private void WriteValue(SourceWriter writer, StorageModel model)
    {
        switch (model.Kind)
        {
            case StorageKind.Object:
                writer.Line("writer.WriteStartObject();");
                WriteMembers(writer, model.Members.Select(member =>
                    ($"value.{Names.Identifier(member.Property)}", member.JsonName, member.Storage)));
                writer.Line("writer.WriteEndObject();");
                break;

            case StorageKind.Map or StorageKind.EnumMap:
                var key = model.Kind == StorageKind.Map ? "entry.Key" : "entry.Key.ToString()";

                writer.Line("writer.WriteStartObject();");

                using (writer.Block("foreach (var entry in value)"))
                {
                    writer.Line($"writer.WritePropertyName({key});");
                    writer.Line(Write(model.Element!, "writer", "entry.Value"));
                }

                writer.Line("writer.WriteEndObject();");
                break;

            default:
                writer.Line("writer.WriteStartArray();");

                using (writer.Block("foreach (var item in value)"))
                {
                    writer.Line(Write(model.Element!, "writer", "item"));
                }

                writer.Line("writer.WriteEndArray();");
                break;
        }
    }

    private void ReadValue(SourceWriter writer, StorageModel model)
    {
        writer.Line("value = null!;");

        if (model.Kind == StorageKind.Object)
        {
            writer.Line($"if (element.ValueKind is not {ObjectKind}) return false;");
            writer.Line();
            writer.Line($"var defaults = new {model.Type}();");
            writer.Line();
            writer.Line($"value = new {model.Type}");

            using (writer.Block(close: "};"))
            {
                writer.List(ReadMembers(
                    model.Members.Select(member =>
                        (Names.Identifier(member.Property), member.JsonName, member.Storage)),
                    "element",
                    property => $"defaults.{property}"));
            }

            writer.Line();
            writer.Line("return true;");
            return;
        }

        var element = model.Element!;
        var reader = Reader(element);

        var read = model.Kind switch
        {
            StorageKind.Map => $"{Json}.TryReadMap<{element.Type}>(element, {reader}, out var items)",
            StorageKind.EnumMap =>
                $"{Json}.TryReadEnumMap<{model.Key}, {element.Type}>(element, {reader}, out var items)",
            _ => $"{Json}.TryReadList<{element.Type}>(element, {reader}, out var items)"
        };

        var converted = model.Kind switch
        {
            StorageKind.Array => "items.ToArray()",
            StorageKind.Set => $"new global::System.Collections.Generic.HashSet<{element.Type}>(items)",
            _ => "items"
        };

        writer.Line($"if (!{read}) return false;");
        writer.Line();
        writer.Line($"value = {converted};");
        writer.Line("return true;");
    }
}
