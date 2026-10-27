using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;
using Weboost.SchemaAnnotations;

namespace Typeshift.Generated;

/// <summary>
/// How generated tools serialize their arguments and results and describe their parameters: the modules' JSON
/// contexts first, then Microsoft.Extensions.AI's defaults, which reflect outside Native AOT.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ToolJson
{
    private readonly ConcurrentDictionary<Type, JsonElement> _schemas = [];
    private readonly AIJsonSchemaCreateOptions _schemaOptions;

    /// <summary>
    /// Creates the options from the resolvers that come first, and the schemas from the docs of the modules.
    /// </summary>
    /// <param name="resolvers">The JSON contexts the modules register, asked in order.</param>
    /// <param name="annotations">The XML docs the modules register, which describe the parameters' properties.</param>
    public ToolJson(IEnumerable<IJsonTypeInfoResolver> resolvers, IEnumerable<ISchemaAnnotationSource> annotations)
    {
        // D45: outside Native AOT the defaults reflect, so tools need no context of their own
        var options = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions)
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                [.. resolvers, AIJsonUtilities.DefaultOptions.TypeInfoResolver])
        };

        options.MakeReadOnly();
        SerializerOptions = options;

        // D55: a <param> doc still describes the parameter itself, since the annotator keeps a description it finds
        _schemaOptions = new AIJsonSchemaCreateOptions
        {
            TransformSchemaNode = new SchemaAnnotator(annotations).Transform
        };
    }

    /// <summary>
    /// The options every tool argument and result is serialized with.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; }

    /// <summary>
    /// Finds the JSON contract of a type.
    /// </summary>
    /// <typeparam name="T">The type to serialize.</typeparam>
    /// <returns>The contract, from the first resolver that knows the type.</returns>
    /// <exception cref="NotSupportedException">
    /// No resolver knows the type: under Native AOT, list it in a <c>JsonSerializerContext</c> of the tool's assembly.
    /// </exception>
    public JsonTypeInfo<T> GetTypeInfo<T>() => (JsonTypeInfo<T>)SerializerOptions.GetTypeInfo(typeof(T));

    // Built once per function type from the STJ contracts, so naming policies and converters match the binding
    internal JsonElement GetSchema(Type function, IReadOnlyList<ToolParameter> parameters) =>
        _schemas.GetOrAdd(
            function,
            static (_, state) => state.Json.CreateSchema(state.Parameters),
            (Json: this, Parameters: parameters));

    private JsonElement CreateSchema(IReadOnlyList<ToolParameter> parameters)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var parameter in parameters)
        {
            properties[parameter.Name] = CreateParameterSchema(parameter);
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

    // System.Type carries no reference nullability, the generator passes it from the parameter's annotation
    private JsonObject CreateParameterSchema(ToolParameter parameter)
    {
        var element = AIJsonUtilities.CreateJsonSchema(
            parameter.Type,
            parameter.Description,
            parameter.HasDefaultValue,
            parameter.DefaultValue,
            SerializerOptions,
            _schemaOptions);

        // A type that takes any JSON gets the schema true, which {} matches
        var schema = element.ValueKind is JsonValueKind.Object ? JsonObject.Create(element)! : new JsonObject();
        RebaseReferences(schema, $"#/properties/{parameter.Name}");
        return parameter.IsNullable ? AsNullable(schema) : schema;
    }

    // A recursive type's $refs point from its own root, which the function's schema nests under the parameter
    private static void RebaseReferences(JsonNode? node, string root)
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

    private static IEnumerable<JsonNode?> GetChildren(JsonNode? node) =>
        node switch
        {
            JsonObject members => members.Select(member => member.Value),
            JsonArray elements => elements,
            _ => []
        };

    // D48: a nullable reference is typed [type, "null"], as the reflection path types it
    private static JsonObject AsNullable(JsonObject schema)
    {
        if (schema["type"] is JsonValue type && type.GetValue<string>() is not "null" and var name)
            schema["type"] = new JsonArray(JsonValue.Create(name), JsonValue.Create("null"));

        return schema;
    }
}
