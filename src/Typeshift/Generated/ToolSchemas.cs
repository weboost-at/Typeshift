using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Weboost.SchemaAnnotations;

namespace Typeshift.Generated;

// The JSON schemas of a container's tools and typed answers, built from the contracts of the tool JSON options, so
// naming policies and converters match the binding
internal sealed class ToolSchemas
{
    private readonly JsonSerializerOptions _serializerOptions;

    public ToolSchemas(JsonSerializerOptions serializerOptions, IEnumerable<ISchemaAnnotationSource> annotations)
    {
        _serializerOptions = serializerOptions;

        // D55: a <param> doc still describes the parameter itself, since the annotator keeps a description it finds
        var annotator = new SchemaAnnotator(annotations);

        CreateOptions = new AIJsonSchemaCreateOptions
        {
            TransformSchemaNode = (context, node) => TypeCases(Supported(context, annotator.Transform(context, node)))
        };
    }

    // What every schema of the container goes through: the XML docs, the check for types the serializer cannot
    // handle, and the type of a union on each case
    public AIJsonSchemaCreateOptions CreateOptions { get; }

    public JsonElement CreateSchema(string tool, IReadOnlyList<ToolParameter> parameters)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var parameter in parameters)
        {
            properties[parameter.Name] = CreateParameterSchema(tool, parameter);
            if (!parameter.HasDefaultValue) required.Add((JsonNode?)JsonValue.Create(parameter.Name));
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Count > 0) schema["required"] = required;

        return JsonSerializer.SerializeToElement(schema, ToolJsonContext.Default.JsonObject);
    }

    // A recursive type's $refs point from its own root, which the function's schema nests under the parameter
    public static void RebaseReferences(JsonNode? node, string root)
    {
        if (node is JsonObject schema && schema["$ref"] is JsonValue reference)
        {
            schema["$ref"] = root + reference.GetValue<string>().TrimStart('#');
        }

        foreach (var child in GetChildren(node))
        {
            RebaseReferences(child, root);
        }
    }

    // System.Type carries no reference nullability, the generator passes it from the parameter's annotation
    private JsonObject CreateParameterSchema(string tool, ToolParameter parameter)
    {
        JsonElement element;

        try
        {
            element = AIJsonUtilities.CreateJsonSchema(
                parameter.Type,
                parameter.Description,
                parameter.HasDefaultValue,
                parameter.DefaultValue,
                _serializerOptions,
                CreateOptions);
        }
        catch (NotSupportedException exception)
        {
            // The schema is built when a run first offers the tool, far from the method that declares the parameter
            throw new NotSupportedException(
                $"The parameter '{parameter.Name}' of the tool '{tool}' has no schema. {exception.Message}",
                exception);
        }

        // A type that takes any JSON gets the schema true, which {} matches
        var schema = element.ValueKind is JsonValueKind.Object ? JsonObject.Create(element)! : new JsonObject();
        RebaseReferences(schema, $"#/properties/{parameter.Name}");
        return parameter.IsNullable ? AsNullable(schema) : schema;
    }

    // D62: the serializer writes a type it cannot read or write, such as the MethodBase of an Exception, as a schema
    // nothing matches, which a provider rejects far from its cause
    private static JsonNode Supported(AIJsonSchemaCreateContext context, JsonNode node)
    {
        if (node is not JsonObject schema || schema["$comment"]?.GetValue<string>() != "Unsupported .NET type")
            return node;

        List<string> names = [];
        var path = context.Path;

        for (var index = 0; index < path.Length - 1; index++)
        {
            if (path[index] == "properties") names.Add(path[index + 1]);
        }

        // [JsonIgnore] goes on a member, so a type the model is asked for directly has to change itself
        var (held, advice) = names.Count == 0
            ? ("", "use another type")
            : ($"'{string.Join(".", names)}', ", "leave it out of the type the model sees, or mark it [JsonIgnore]");

        throw new NotSupportedException(
            $"The model's schema cannot hold {held}a {context.TypeInfo.Type.Name}, which System.Text.Json cannot read "
            + $"or write: {advice}.");
    }

    // D62: the serializer names a type the cases of a union share once, on the union, and providers look for it on
    // each case
    private static JsonNode TypeCases(JsonNode node)
    {
        if (node is not JsonObject schema || schema["anyOf"] is not JsonArray cases || schema["type"] is not { } type)
            return node;

        foreach (var @case in cases.OfType<JsonObject>().Where(@case => !@case.ContainsKey("type")))
        {
            @case["type"] = type.DeepClone();
        }

        return node;
    }

    private static IEnumerable<JsonNode?> GetChildren(JsonNode? node) =>
        node switch
        {
            JsonObject members => members.Select(member => member.Value),
            JsonArray elements => elements,
            _ => []
        };

    // D48: a nullable reference is typed [type, "null"], as the reflection path types it, and so is each case of a
    // union (D62)
    private static JsonObject AsNullable(JsonObject schema)
    {
        if (schema["type"] is JsonValue type && type.GetValue<string>() is not "null" and var name)
            schema["type"] = new JsonArray(JsonValue.Create(name), JsonValue.Create("null"));

        foreach (var @case in (schema["anyOf"] as JsonArray ?? []).OfType<JsonObject>())
        {
            AsNullable(@case);
        }

        return schema;
    }
}
