using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
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
    private readonly ConcurrentDictionary<Type, ResultFormat> _results = [];
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

        options.TypeClassifiers.Add(new UnionCaseClassifier(options));

        options.MakeReadOnly();
        SerializerOptions = options;

        // D55: a <param> doc still describes the parameter itself, since the annotator keeps a description it finds
        var annotator = new SchemaAnnotator(annotations);

        _schemaOptions = new AIJsonSchemaCreateOptions
        {
            TransformSchemaNode = (context, node) => TypeCases(Supported(context, annotator.Transform(context, node)))
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

    // D62: built once per type as a tool's parameters are, and held under data unless it is an object of properties,
    // since providers take nothing else as an answer's schema
    internal ResultFormat GetResultFormat<T>() =>
        _results.GetOrAdd(typeof(T), static (_, json) => json.CreateResultFormat<T>(), this);

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

    private ResultFormat CreateResultFormat<T>()
    {
        var element = AIJsonUtilities.CreateJsonSchema(
            typeof(T),
            serializerOptions: SerializerOptions,
            inferenceOptions: _schemaOptions);

        var schema = element.ValueKind is JsonValueKind.Object ? JsonObject.Create(element)! : new JsonObject();
        var wrapped = !IsObjectOfProperties(schema);
        if (wrapped) schema = UnderData(schema);

        var format = ChatResponseFormat.ForJsonSchema(
            JsonSerializer.SerializeToElement(schema, ToolJsonContext.Default.JsonObject),
            typeof(T).GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? SchemaName(typeof(T)),
            typeof(T).GetCustomAttribute<DescriptionAttribute>()?.Description);

        return new ResultFormat(format, wrapped ? WithResultUnderData<T>() : SerializerOptions);
    }

    private static bool IsObjectOfProperties(JsonObject schema) =>
        schema["type"] is JsonValue type
        && type.GetValue<string>() is "object"
        && schema["properties"] is JsonObject
        && !schema.ContainsKey("anyOf");

    private static JsonObject UnderData(JsonObject schema)
    {
        RebaseReferences(schema, "#/properties/data");

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { ["data"] = schema },
            ["additionalProperties"] = false,
            ["required"] = new JsonArray(JsonValue.Create("data"))
        };
    }

    // As Microsoft.Extensions.AI names the schema of a typed answer
    private static string SchemaName(Type type) => Regex.Replace(type.Name, "[^0-9A-Za-z_]", "_");

    // A contract of its own for T, since a converter in the options loses to an enum's converter and a polymorphic
    // type's metadata
    private JsonSerializerOptions WithResultUnderData<T>()
    {
        var options = new JsonSerializerOptions(SerializerOptions)
        {
            TypeInfoResolver = new DataResolver<T>(SerializerOptions.TypeInfoResolver!, GetTypeInfo<T>())
        };

        options.MakeReadOnly();
        return options;
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

        var member = names.Count == 0 ? context.TypeInfo.Type.Name : string.Join(".", names);

        throw new NotSupportedException(
            $"The model's schema cannot hold '{member}', a {context.TypeInfo.Type.Name}, which System.Text.Json "
            + "cannot read or write: leave it out of the type the model sees, or mark it [JsonIgnore].");
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

    private sealed class DataResolver<T>(IJsonTypeInfoResolver resolver, JsonTypeInfo<T> result) : IJsonTypeInfoResolver
    {
        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            if (type != typeof(T)) return resolver.GetTypeInfo(type, options);

            var info = JsonMetadataServices.CreateValueInfo<T>(options, new DataConverter<T>(result));
            info.PolymorphismOptions = null;
            return info;
        }
    }

    // A typed answer under data, read as Microsoft.Extensions.AI reads one it wrapped itself
    private sealed class DataConverter<T>(JsonTypeInfo<T> result) : JsonConverter<T>
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType is not JsonTokenType.StartObject)
                throw new JsonException("The answer is no JSON object holding a data property.");

            while (reader.Read() && reader.TokenType is JsonTokenType.PropertyName)
            {
                var isData = reader.ValueTextEquals("data"u8);
                reader.Read();

                if (isData) return Finish(ref reader, JsonSerializer.Deserialize(ref reader, result));

                reader.Skip();
            }

            throw new JsonException("The answer holds no data property.");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("data"u8);
            JsonSerializer.Serialize(writer, value, result);
            writer.WriteEndObject();
        }

        // A converter leaves the reader on the end of its value
        private static T? Finish(ref Utf8JsonReader reader, T? value)
        {
            while (reader.Read() && reader.TokenType is JsonTokenType.PropertyName)
            {
                reader.Read();
                reader.Skip();
            }

            return value;
        }
    }
}

// A typed run's response format, and the options that read its answer
internal sealed record ResultFormat(ChatResponseFormatJson Format, JsonSerializerOptions SerializerOptions);
